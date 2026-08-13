using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Topics;
using Nop.Data;
using Nop.Services.Localization;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

/// <summary>
/// Idempotently installs the reviewed localization package carried inside the
/// plugin assembly. Entity ids are validated before any value is written, so a
/// package cannot silently target the wrong catalog after an upgrade.
/// </summary>
public interface ILocalizationResourceInstaller
{
    Task UpsertAsync(IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> resourcesByLanguageId);
    Task InstallEmbeddedPackageAsync();
}

public sealed class LocalizationResourceInstaller : ILocalizationResourceInstaller
{
    private const string ResourcePrefix = "Nop.Plugin.Misc.HoodLocalizationSeo.Resources.";
    private readonly IStaticCacheManager _cacheManager;
    private readonly IRepository<BlogPost> _blogPostRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly ILanguageService _languageService;
    private readonly IRepository<LocaleStringResource> _localeStringResourceRepository;
    private readonly IRepository<LocalizedProperty> _localizedPropertyRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<ProductAttribute> _productAttributeRepository;
    private readonly IRepository<ProductAttributeMapping> _productAttributeMappingRepository;
    private readonly IRepository<ProductAttributeValue> _productAttributeValueRepository;
    private readonly IRepository<ProductTag> _productTagRepository;
    private readonly IRepository<Store> _storeRepository;
    private readonly ISitemapArtifactInvalidator _sitemapArtifactInvalidator;
    private readonly IRepository<Topic> _topicRepository;
    private readonly IRepository<UrlRecord> _urlRecordRepository;

    public LocalizationResourceInstaller(IStaticCacheManager cacheManager,
        IRepository<BlogPost> blogPostRepository,
        IRepository<Category> categoryRepository,
        ILanguageService languageService,
        IRepository<LocaleStringResource> localeStringResourceRepository,
        IRepository<LocalizedProperty> localizedPropertyRepository,
        IRepository<Product> productRepository,
        IRepository<ProductAttribute> productAttributeRepository,
        IRepository<ProductAttributeMapping> productAttributeMappingRepository,
        IRepository<ProductAttributeValue> productAttributeValueRepository,
        IRepository<ProductTag> productTagRepository,
        IRepository<Store> storeRepository,
        ISitemapArtifactInvalidator sitemapArtifactInvalidator,
        IRepository<Topic> topicRepository,
        IRepository<UrlRecord> urlRecordRepository)
    {
        _cacheManager = cacheManager;
        _blogPostRepository = blogPostRepository;
        _categoryRepository = categoryRepository;
        _languageService = languageService;
        _localeStringResourceRepository = localeStringResourceRepository;
        _localizedPropertyRepository = localizedPropertyRepository;
        _productRepository = productRepository;
        _productAttributeRepository = productAttributeRepository;
        _productAttributeMappingRepository = productAttributeMappingRepository;
        _productAttributeValueRepository = productAttributeValueRepository;
        _productTagRepository = productTagRepository;
        _storeRepository = storeRepository;
        _sitemapArtifactInvalidator = sitemapArtifactInvalidator;
        _topicRepository = topicRepository;
        _urlRecordRepository = urlRecordRepository;
    }

    public async Task UpsertAsync(IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> resourcesByLanguageId)
    {
        ArgumentNullException.ThrowIfNull(resourcesByLanguageId);

        foreach (var (languageId, resources) in resourcesByLanguageId)
        {
            if (languageId <= 0 || resources.Count == 0)
                continue;

            // nopCommerce normalizes batch resource names with ToLowerInvariant().
            // On databases using Turkish_CI_AS, ASCII I/i do not compare as the
            // same character, so a legacy mixed-case row and the normalized row
            // can coexist. Load this language's keys once and reconcile the
            // plugin-owned subset in memory with ordinal invariant semantics.
            var desired = resources
                .Select(pair => new
                {
                    Name = NormalizeResourceName(pair.Key),
                    pair.Value
                })
                .ToList();
            var duplicateDesired = desired.GroupBy(item => item.Name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() != 1);
            if (duplicateDesired is not null)
                throw new InvalidDataException($"Duplicate normalized UI resource key: {duplicateDesired.Key}");

            var desiredByName = desired.ToDictionary(item => item.Name, item => item.Value,
                StringComparer.Ordinal);
            var existingForLanguage = await _localeStringResourceRepository.GetAllAsync(query =>
                query.Where(item => item.LanguageId == languageId));
            var ownedExisting = existingForLanguage
                .Where(item => desiredByName.ContainsKey(NormalizeResourceName(item.ResourceName)))
                .GroupBy(item => NormalizeResourceName(item.ResourceName), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Id).ToList(),
                    StringComparer.Ordinal);

            var inserts = new List<LocaleStringResource>();
            var updates = new List<LocaleStringResource>();
            var duplicatesToDelete = new List<LocaleStringResource>();
            foreach (var (name, value) in desiredByName)
            {
                if (!ownedExisting.TryGetValue(name, out var matches))
                {
                    inserts.Add(new LocaleStringResource
                    {
                        LanguageId = languageId,
                        ResourceName = name,
                        ResourceValue = value
                    });
                    continue;
                }

                var keeper = matches.FirstOrDefault(item =>
                                 item.ResourceName.Equals(name, StringComparison.Ordinal)) ??
                             matches[0];
                duplicatesToDelete.AddRange(matches.Where(item => item.Id != keeper.Id));
                if (!keeper.ResourceName.Equals(name, StringComparison.Ordinal) || keeper.ResourceValue != value)
                {
                    keeper.ResourceName = name;
                    keeper.ResourceValue = value;
                    updates.Add(keeper);
                }
            }

            if (duplicatesToDelete.Count > 0)
                await _localeStringResourceRepository.DeleteAsync(duplicatesToDelete);
            if (updates.Count > 0)
                await _localeStringResourceRepository.UpdateAsync(updates, false);
            if (inserts.Count > 0)
                await _localeStringResourceRepository.InsertAsync(inserts, false);
        }
    }

    private static string NormalizeResourceName(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            throw new InvalidDataException("UI resource name cannot be blank.");

        return resourceName.Trim().ToLowerInvariant();
    }

    public async Task InstallEmbeddedPackageAsync()
    {
        var package = ReadAndValidateEmbeddedPackage();
        var qualityGate = package.QualityGate;
        var manifest = package.Manifest;
        var uiResources = package.UiResources;
        var productTags = package.ProductTags;
        var productAttributes = package.ProductAttributes;
        var productProseKoc = package.ProductProseKoc;
        var productProseSupplemental = package.ProductProseSupplemental;
        var languageIdsByCode = await ResolveLanguageIdsAsync(manifest, uiResources,
            productTags, productAttributes);

        var localizedRows = manifest.Entries.SelectMany(entry => entry.Fields.Select(field => new LocalizedRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                Group = entry.EntityType,
                Key = field.Key,
                Value = field.Value
            }))
            .Concat(productTags.Values.Select(entry => new LocalizedRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                Group = "ProductTag",
                Key = "Name",
                Value = entry.Value
            }))
            .Concat(productAttributes.Rows.Select(entry => new LocalizedRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                Group = entry.Group,
                Key = entry.Key,
                Value = entry.Value
            }))
            .ToList();
        var slugs = manifest.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Slug))
            .Select(entry => new SlugRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                EntityName = entry.EntityType,
                Slug = entry.Slug,
                PreviousSlugs = entry.PreviousSlugs
            })
            .Concat(productTags.Slugs.Select(entry => new SlugRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                EntityName = "ProductTag",
                Slug = entry.Slug,
                PreviousSlugs = entry.PreviousSlugs
            }))
            .ToList();
        MergeSupplementalProductSlugs(slugs, productProseSupplemental,
            languageIdsByCode);

        ValidateNoProductProseTupleOverlap(localizedRows, productProseKoc.Rows,
            languageIdsByCode);
        ValidateNoSupplementalProductProseTupleOverlap(localizedRows, productProseKoc.Rows,
            productProseSupplemental, languageIdsByCode);

        await ValidateEntityTargetsAsync(manifest, productTags, productAttributes,
            localizedRows, slugs, manifest.RetiredUrlRecords);

        var resourcesByLanguage = uiResources.Entries
            .GroupBy(entry => ResolveLanguageId(languageIdsByCode, entry.LanguageCode))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, string>)group.ToDictionary(
                    entry => entry.ResourceName,
                    entry => entry.ResourceValue,
                    StringComparer.OrdinalIgnoreCase));
        using (var transaction = new TransactionScope(TransactionScopeOption.Required,
                   TimeSpan.FromMinutes(20), TransactionScopeAsyncFlowOption.Enabled))
        {
            var preparedProductProse = await PrepareProductProseCorrectionsAsync(productProseKoc,
                languageIdsByCode);
            // Prepare and validate every correction package before the first write. This keeps
            // a drifted supplemental row from leaving the main package partially installed.
            var preparedSupplementalProductProse =
                await PrepareSupplementalProductProseCorrectionsAsync(productProseSupplemental,
                    languageIdsByCode);
            await UpsertAsync(resourcesByLanguage);
            await UpsertLocalizedPropertiesAsync(localizedRows);
            await UpsertSlugsAsync(slugs, manifest.RetiredUrlRecords);
            await ApplyPreparedProductProseCorrectionsAsync(preparedProductProse);
            await ApplyPreparedProductProseCorrectionsAsync(preparedSupplementalProductProse);
            transaction.Complete();
        }
        await _cacheManager.ClearAsync();
        _sitemapArtifactInvalidator.InvalidateGeneratedXmlFiles();
    }

    internal static void ValidateEmbeddedPackagePreflight() => _ = ReadAndValidateEmbeddedPackage();

    internal static (ProductProseKocPackage ProductProse, ProductTagPackage ProductTags)
        ReadEmbeddedProductProseKocValidationFixture() =>
        (ReadEmbedded<ProductProseKocPackage>("product-prose-koc-corrections.json"),
            ReadEmbedded<ProductTagPackage>("product-tag-localization.json"));

    internal static (ProductProseCanonicalLinguisticQa Qa, ProductProseKocPackage ProductProse,
        ProductTagPackage ProductTags) ReadEmbeddedProductProseLinguisticQaValidationFixture() =>
        (ReadEmbedded<ProductProseCanonicalLinguisticQa>(
                "product-prose-canonical-linguistic-independent-qa.json"),
            ReadEmbedded<ProductProseKocPackage>("product-prose-koc-corrections.json"),
            ReadEmbedded<ProductTagPackage>("product-tag-localization.json"));

    internal static ProductProseSupplementalPackage
        ReadEmbeddedProductProseSupplementalValidationFixture() =>
        ReadEmbedded<ProductProseSupplementalPackage>(
            "product-prose-supplemental-corrections.json");

    internal static void MergeSupplementalProductSlugs(IList<SlugRow> slugs,
        ProductProseSupplementalPackage package,
        IReadOnlyDictionary<string, int> languageIdsByCode)
    {
        ArgumentNullException.ThrowIfNull(slugs);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(languageIdsByCode);

        var supplementalRows = package.Slugs.Select(entry => new SlugRow
            {
                EntityId = entry.EntityId,
                LanguageId = ResolveLanguageId(languageIdsByCode, entry.LanguageCode),
                EntityName = nameof(Product),
                Slug = entry.Slug,
                PreviousSlugs = entry.PreviousSlugs
            })
            .ToList();
        var occupied = slugs.Select(row =>
                (EntityName: row.EntityName.ToLowerInvariant(), row.EntityId, row.LanguageId))
            .ToHashSet();
        var overlap = supplementalRows.FirstOrDefault(row => occupied.Contains(
            (row.EntityName.ToLowerInvariant(), row.EntityId, row.LanguageId)));
        if (overlap is not null)
            throw new InvalidDataException(
                $"Supplemental product slug overlaps another package: " +
                $"{overlap.EntityName}/{overlap.EntityId}/{overlap.LanguageId}.");

        foreach (var row in supplementalRows)
            slugs.Add(row);
    }

    private static EmbeddedLocalizationPackage ReadAndValidateEmbeddedPackage()
    {
        var qualityGate = ReadEmbedded<LocalizationQualityGate>("localization-quality-gate.json");
        ValidateQualityGate(qualityGate);
        var manifest = ReadEmbedded<LocalizationManifest>("localization-manifest.json");
        var uiResources = ReadEmbedded<UiResourcePackage>("ui-localization-resources.json");
        var productTags = ReadEmbedded<ProductTagPackage>("product-tag-localization.json");
        var productAttributes = ReadEmbedded<ProductAttributePackage>("product-attribute-localization.json");
        var productProseKoc = ReadEmbedded<ProductProseKocPackage>("product-prose-koc-corrections.json");
        var productProseSupplemental = ReadEmbedded<ProductProseSupplementalPackage>(
            "product-prose-supplemental-corrections.json");
        var productProseLinguisticQa = ReadEmbedded<ProductProseCanonicalLinguisticQa>(
            "product-prose-canonical-linguistic-independent-qa.json");
        ValidatePackage(qualityGate, manifest, uiResources, productTags, productAttributes);
        ValidateProductProseKocPackage(productProseKoc, productTags);
        ValidateProductProseSupplementalPackage(productProseSupplemental);
        ValidateProductProseCanonicalLinguisticQa(productProseLinguisticQa, productProseKoc,
            productTags, MatchesEmbeddedSha256);

        return new EmbeddedLocalizationPackage(qualityGate, manifest, uiResources, productTags,
            productAttributes, productProseKoc, productProseSupplemental);
    }

    private async Task<IReadOnlyDictionary<string, int>> ResolveLanguageIdsAsync(
        LocalizationManifest manifest, UiResourcePackage uiResources,
        ProductTagPackage productTags, ProductAttributePackage productAttributes)
    {
        var packageCodeSets = new[]
        {
            manifest.Entries.Select(entry => entry.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
            uiResources.Entries.Select(entry => entry.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
            productTags.Values.Select(entry => entry.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
            productTags.Slugs.Select(entry => entry.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase),
            productAttributes.Rows.Select(entry => entry.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
        var expectedCodes = packageCodeSets[0];
        if (expectedCodes.Count != 24 || packageCodeSets.Any(codes => !codes.SetEquals(expectedCodes)) ||
            expectedCodes.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Localization packages do not contain the same 24 language codes.");

        var installedLanguages = await _languageService.GetAllLanguagesAsync(showHidden: true);
        var duplicateCode = installedLanguages
            .Where(language => !string.IsNullOrWhiteSpace(language.UniqueSeoCode))
            .GroupBy(language => language.UniqueSeoCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
            throw new InvalidDataException($"Installed languages contain duplicate SEO code '{duplicateCode.Key}'.");

        var installedByCode = installedLanguages
            .Where(language => !string.IsNullOrWhiteSpace(language.UniqueSeoCode))
            .ToDictionary(language => language.UniqueSeoCode.Trim(), language => language.Id,
                StringComparer.OrdinalIgnoreCase);
        var missingCodes = expectedCodes.Where(code => !installedByCode.ContainsKey(code)).OrderBy(code => code).ToArray();
        if (missingCodes.Length > 0)
            throw new InvalidDataException(
                $"Localization package requires missing language codes: {string.Join(',', missingCodes)}");

        return expectedCodes.ToDictionary(code => code, code => installedByCode[code],
            StringComparer.OrdinalIgnoreCase);
    }

    private static int ResolveLanguageId(IReadOnlyDictionary<string, int> languageIdsByCode,
        string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            !languageIdsByCode.TryGetValue(languageCode, out var languageId))
            throw new InvalidDataException($"Unknown package language code '{languageCode}'.");
        return languageId;
    }

    private async Task UpsertLocalizedPropertiesAsync(IList<LocalizedRow> rows)
    {
        var duplicate = rows.GroupBy(row => (row.EntityId, row.LanguageId,
                Group: row.Group.ToLowerInvariant(), Key: row.Key.ToLowerInvariant()))
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Duplicate localized-property package key: {duplicate.Key}");

        foreach (var group in rows.GroupBy(row => (row.Group, row.Key),
                     new LocalizedGroupKeyComparer()))
        {
            var languageIds = group.Select(row => row.LanguageId).Distinct().ToArray();
            var existing = await _localizedPropertyRepository.GetAllAsync(query => query.Where(item =>
                languageIds.Contains(item.LanguageId) &&
                item.LocaleKeyGroup == group.Key.Group && item.LocaleKey == group.Key.Key));
            var index = existing.ToDictionary(item => (item.EntityId, item.LanguageId));
            var inserts = new List<LocalizedProperty>();
            var updates = new List<LocalizedProperty>();
            foreach (var row in group)
            {
                if (index.TryGetValue((row.EntityId, row.LanguageId), out var current))
                {
                    if (current.LocaleValue == row.Value)
                        continue;
                    current.LocaleValue = row.Value;
                    updates.Add(current);
                }
                else
                {
                    inserts.Add(new LocalizedProperty
                    {
                        EntityId = row.EntityId,
                        LanguageId = row.LanguageId,
                        LocaleKeyGroup = row.Group,
                        LocaleKey = row.Key,
                        LocaleValue = row.Value
                    });
                }
            }
            if (inserts.Count > 0)
                await _localizedPropertyRepository.InsertAsync(inserts, false);
            if (updates.Count > 0)
                await _localizedPropertyRepository.UpdateAsync(updates, false);
        }
    }

    private async Task<PreparedProductProseCorrections> PrepareProductProseCorrectionsAsync(
        ProductProseKocPackage package, IReadOnlyDictionary<string, int> languageIdsByCode)
    {
        var allProductIds = package.Rows.Select(row => row.EntityId)
            .Concat(package.PreservedFalsePositives.Select(row => row.EntityId))
            .Distinct()
            .ToArray();
        var products = await _productRepository.GetAllAsync(query => query.Where(product =>
            allProductIds.Contains(product.Id)));
        var productById = products.ToDictionary(product => product.Id);
        if (productById.Count != allProductIds.Length)
            throw new InvalidDataException("Product-prose correction targets no longer exist.");

        foreach (var identity in package.TargetProducts)
        {
            if (!productById.TryGetValue(identity.EntityId, out var product) ||
                !string.Equals(product.Sku, identity.Sku, StringComparison.Ordinal))
                throw new InvalidDataException($"Product-prose identity witness changed: {identity.EntityId}.");
        }

        var localizedRows = package.Rows.Where(row =>
            row.TargetKind.Equals("LocalizedProperty", StringComparison.Ordinal)).ToList();
        var localizedEntityIds = localizedRows.Select(row => row.EntityId)
            .Concat(package.PreservedFalsePositives.Select(row => row.EntityId))
            .Distinct()
            .ToArray();
        var localizedLanguageIds = localizedRows.Select(row => ResolveLanguageId(languageIdsByCode,
                row.LanguageCode))
            .Concat(package.PreservedFalsePositives.Select(row =>
                ResolveLanguageId(languageIdsByCode, row.LanguageCode)))
            .Distinct()
            .ToArray();
        var existingLocalized = await _localizedPropertyRepository.GetAllAsync(query => query.Where(item =>
            localizedEntityIds.Contains(item.EntityId) && localizedLanguageIds.Contains(item.LanguageId) &&
            item.LocaleKeyGroup == "Product" &&
            (item.LocaleKey == "ShortDescription" || item.LocaleKey == "FullDescription")));
        var groupedLocalized = existingLocalized.GroupBy(item =>
                (item.EntityId, item.LanguageId, item.LocaleKey))
            .ToDictionary(group => group.Key, group => group.ToList());
        if (groupedLocalized.Any(pair => pair.Value.Count != 1))
            throw new InvalidDataException("Product-prose correction target contains duplicate localized rows.");

        var prepared = new PreparedProductProseCorrections();
        foreach (var row in package.Rows)
        {
            if (!productById.TryGetValue(row.EntityId, out var product) ||
                !string.Equals(product.Sku, row.Sku, StringComparison.Ordinal))
                throw new InvalidDataException($"Product-prose row identity changed: {row.EntityId}.");

            if (row.TargetKind.Equals("InvariantProduct", StringComparison.Ordinal))
            {
                var currentValue = row.Field.Equals("ShortDescription", StringComparison.Ordinal)
                    ? product.ShortDescription
                    : product.FullDescription;
                var desiredValue = ResolveProductProseCorrectionValue(currentValue, row);
                if (!string.Equals(currentValue, desiredValue, StringComparison.Ordinal))
                    prepared.ProductActions.Add(new ProductProseProductAction(product, row.Field, desiredValue));
                continue;
            }

            var languageId = ResolveLanguageId(languageIdsByCode, row.LanguageCode);
            if (!groupedLocalized.TryGetValue((row.EntityId, languageId, row.Field), out var matches) ||
                matches.Count != 1)
                throw new InvalidDataException(
                    $"Product-prose localized target is missing: {row.EntityId}/{row.LanguageCode}/{row.Field}.");
            var current = matches[0];
            var desired = ResolveProductProseCorrectionValue(current.LocaleValue, row);
            if (!string.Equals(current.LocaleValue, desired, StringComparison.Ordinal))
                prepared.LocalizedActions.Add(new ProductProseLocalizedAction(current, desired));
        }

        foreach (var witness in package.PreservedFalsePositives)
        {
            if (!productById.TryGetValue(witness.EntityId, out var product) ||
                !string.Equals(product.Sku, witness.Sku, StringComparison.Ordinal))
                throw new InvalidDataException($"Product-prose preserved witness identity changed: {witness.EntityId}.");
            var languageId = ResolveLanguageId(languageIdsByCode, witness.LanguageCode);
            if (!groupedLocalized.TryGetValue((witness.EntityId, languageId, witness.Field), out var matches) ||
                matches.Count != 1 || !string.Equals(matches[0].LocaleValue, witness.ExpectedValue,
                    StringComparison.Ordinal) ||
                !Sha256(matches[0].LocaleValue).Equals(witness.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Product-prose preserved false-positive witness changed: " +
                    $"{witness.EntityId}/{witness.LanguageCode}/{witness.Field}.");
        }

        return prepared;
    }

    private async Task ApplyPreparedProductProseCorrectionsAsync(PreparedProductProseCorrections prepared)
    {
        var productUpdates = new HashSet<Product>();
        foreach (var action in prepared.ProductActions)
        {
            if (action.Field.Equals("ShortDescription", StringComparison.Ordinal))
                action.Product.ShortDescription = action.Value;
            else
                action.Product.FullDescription = action.Value;
            productUpdates.Add(action.Product);
        }
        foreach (var action in prepared.LocalizedActions)
            action.Row.LocaleValue = action.Value;

        if (productUpdates.Count > 0)
            await _productRepository.UpdateAsync(productUpdates.ToList(), false);
        if (prepared.LocalizedActions.Count > 0)
            await _localizedPropertyRepository.UpdateAsync(
                prepared.LocalizedActions.Select(action => action.Row).ToList(), false);
    }

    private async Task<PreparedProductProseCorrections>
        PrepareSupplementalProductProseCorrectionsAsync(ProductProseSupplementalPackage package,
            IReadOnlyDictionary<string, int> languageIdsByCode)
    {
        var allProductIds = package.Rows.Select(row => row.EntityId)
            .Concat(package.PreservedWitnesses.Select(row => row.EntityId))
            .Distinct()
            .ToArray();
        var products = await _productRepository.GetAllAsync(query => query.Where(product =>
            allProductIds.Contains(product.Id)));
        var productById = products.ToDictionary(product => product.Id);
        if (productById.Count != allProductIds.Length)
            throw new InvalidDataException("Supplemental product-prose targets no longer exist.");

        var languageIds = package.Rows.Select(row => ResolveLanguageId(languageIdsByCode,
                row.LanguageCode))
            .Concat(package.PreservedWitnesses.Select(row =>
                ResolveLanguageId(languageIdsByCode, row.LanguageCode)))
            .Distinct()
            .ToArray();
        var existing = await _localizedPropertyRepository.GetAllAsync(query => query.Where(item =>
            allProductIds.Contains(item.EntityId) && languageIds.Contains(item.LanguageId) &&
            item.LocaleKeyGroup == "Product" &&
            (item.LocaleKey == "Name" || item.LocaleKey == "ShortDescription" ||
             item.LocaleKey == "FullDescription")));
        var existingByTuple = existing.GroupBy(item =>
                (item.EntityId, item.LanguageId, item.LocaleKey))
            .ToDictionary(group => group.Key, group => group.ToList());
        if (existingByTuple.Any(pair => pair.Value.Count != 1))
            throw new InvalidDataException(
                "Supplemental product-prose target contains duplicate localized rows.");

        var prepared = new PreparedProductProseCorrections();
        foreach (var row in package.Rows)
        {
            ValidateSupplementalProductIdentity(productById, row.EntityId, row.Sku,
                "correction");
            var languageId = ResolveLanguageId(languageIdsByCode, row.LanguageCode);
            if (!existingByTuple.TryGetValue((row.EntityId, languageId, row.Field), out var matches) ||
                matches.Count != 1)
                throw new InvalidDataException(
                    $"Supplemental product-prose target is missing: " +
                    $"{row.EntityId}/{row.LanguageCode}/{row.Field}.");
            var current = matches[0];
            var desired = ResolveSupplementalProductProseCorrectionValue(current.LocaleValue, row);
            if (!string.Equals(current.LocaleValue, desired, StringComparison.Ordinal))
                prepared.LocalizedActions.Add(new ProductProseLocalizedAction(current, desired));
        }

        foreach (var witness in package.PreservedWitnesses)
        {
            ValidateSupplementalProductIdentity(productById, witness.EntityId, witness.Sku,
                "preserved witness");
            var languageId = ResolveLanguageId(languageIdsByCode, witness.LanguageCode);
            if (!existingByTuple.TryGetValue((witness.EntityId, languageId, witness.Field),
                    out var matches) || matches.Count != 1 ||
                !string.Equals(matches[0].LocaleValue, witness.ExpectedValue,
                    StringComparison.Ordinal) ||
                !Sha256(matches[0].LocaleValue).Equals(witness.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Supplemental product-prose preserved witness changed: " +
                    $"{witness.EntityId}/{witness.LanguageCode}/{witness.Field}.");
        }

        return prepared;
    }

    private static void ValidateSupplementalProductIdentity(
        IReadOnlyDictionary<int, Product> products, int entityId, string sku, string kind)
    {
        if (!products.TryGetValue(entityId, out var product) ||
            !string.Equals(product.Sku, sku, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Supplemental product-prose {kind} identity changed: {entityId}.");
    }

    internal static string ResolveSupplementalProductProseCorrectionValue(string currentValue,
        ProductProseSupplementalCorrection row)
    {
        currentValue ??= string.Empty;
        if (string.Equals(currentValue, row.NewValue, StringComparison.Ordinal) &&
            Sha256(currentValue).Equals(row.NewSha256, StringComparison.OrdinalIgnoreCase))
            return currentValue;
        if (string.Equals(currentValue, row.OldValue, StringComparison.Ordinal) &&
            Sha256(currentValue).Equals(row.OldSha256, StringComparison.OrdinalIgnoreCase))
            return row.NewValue;
        throw new InvalidDataException(
            $"Supplemental product-prose correction drifted: LocalizedProperty/" +
            $"{row.EntityId}/{row.LanguageCode}/{row.Field}.");
    }

    internal static string ResolveProductProseCorrectionValue(string currentValue,
        ProductProseKocCorrection row)
    {
        ValidateProductProseAcceptedPreviousValues(row);
        currentValue ??= string.Empty;
        if (string.Equals(currentValue, row.NewValue, StringComparison.Ordinal) &&
            Sha256(currentValue).Equals(row.NewSha256, StringComparison.OrdinalIgnoreCase))
            return currentValue;
        if (string.Equals(currentValue, row.OldValue, StringComparison.Ordinal) &&
            Sha256(currentValue).Equals(row.OldSha256, StringComparison.OrdinalIgnoreCase))
            return row.NewValue;
        if (row.AcceptedPreviousValues.Any(previous =>
                string.Equals(currentValue, previous.Value, StringComparison.Ordinal) &&
                Sha256(currentValue).Equals(previous.Sha256, StringComparison.OrdinalIgnoreCase)))
            return row.NewValue;
        throw new InvalidDataException(
            $"Product-prose correction drifted: {row.TargetKind}/{row.EntityId}/" +
            $"{row.LanguageCode ?? "invariant"}/{row.Field}.");
    }

    internal static void ValidateProductProseAcceptedPreviousValues(
        ProductProseKocCorrection row)
    {
        if (row.AcceptedPreviousValues is null)
            throw new InvalidDataException("Product-prose accepted previous values are missing.");
        if (row.AcceptedPreviousValues.Count == 0)
            return;

        const string reviewedPreviousSha256 =
            "803B17867E4BC9E7CC8BB8A09609F8CBA2B0AB5D7F6B42EDF7628E97088E7BC9";
        if (row.AcceptedPreviousValues.Count != 1 ||
            !string.Equals(row.TargetKind, "LocalizedProperty", StringComparison.Ordinal) ||
            row.EntityId != 169 || !string.Equals(row.Sku, "arrow34", StringComparison.Ordinal) ||
            !string.Equals(row.LanguageCode, "ur", StringComparison.Ordinal) ||
            !string.Equals(row.Field, "FullDescription", StringComparison.Ordinal))
            throw new InvalidDataException(
                "Product-prose accepted previous value is not explicitly allowlisted.");

        var previous = row.AcceptedPreviousValues[0];
        if (previous is null || string.IsNullOrWhiteSpace(previous.Value) ||
            !string.Equals(previous.Sha256, reviewedPreviousSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !Sha256(previous.Value).Equals(previous.Sha256,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(previous.Value, row.OldValue, StringComparison.Ordinal) ||
            string.Equals(previous.Value, row.NewValue, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Product-prose accepted previous value evidence is invalid.");
    }

    internal static void ValidateNoProductProseTupleOverlap(IList<LocalizedRow> regularRows,
        IList<ProductProseKocCorrection> proseRows,
        IReadOnlyDictionary<string, int> languageIdsByCode)
    {
        var regularProductKeys = regularRows
            .Where(row => row.Group.Equals("Product", StringComparison.OrdinalIgnoreCase))
            .Select(row => (row.EntityId, row.LanguageId, Key: row.Key.ToLowerInvariant()))
            .ToHashSet();
        var overlap = proseRows.Where(row =>
                row.TargetKind.Equals("LocalizedProperty", StringComparison.Ordinal))
            .Select(row => (row.EntityId,
                LanguageId: ResolveLanguageId(languageIdsByCode, row.LanguageCode),
                Key: row.Field.ToLowerInvariant()))
            .FirstOrDefault(regularProductKeys.Contains);
        if (overlap != default)
            throw new InvalidDataException(
                $"Regular localization and product-prose packages overlap: {overlap}.");
    }

    internal static void ValidateNoSupplementalProductProseTupleOverlap(
        IList<LocalizedRow> regularRows, IList<ProductProseKocCorrection> mainRows,
        ProductProseSupplementalPackage supplemental,
        IReadOnlyDictionary<string, int> languageIdsByCode)
    {
        var occupied = regularRows
            .Where(row => row.Group.Equals("Product", StringComparison.OrdinalIgnoreCase))
            .Select(row => (row.EntityId, row.LanguageId, Key: row.Key.ToLowerInvariant()))
            .Concat(mainRows.Where(row =>
                    row.TargetKind.Equals("LocalizedProperty", StringComparison.Ordinal))
                .Select(row => (row.EntityId,
                    LanguageId: ResolveLanguageId(languageIdsByCode, row.LanguageCode),
                    Key: row.Field.ToLowerInvariant())))
            .ToHashSet();
        var overlap = supplemental.Rows.Select(row => (row.EntityId,
                LanguageId: ResolveLanguageId(languageIdsByCode, row.LanguageCode),
                Key: row.Field.ToLowerInvariant()))
            .FirstOrDefault(occupied.Contains);
        if (overlap != default)
            throw new InvalidDataException(
                $"Supplemental product-prose package overlaps another package: {overlap}.");
        var witnessOverlap = supplemental.PreservedWitnesses.Select(row => (row.EntityId,
                LanguageId: ResolveLanguageId(languageIdsByCode, row.LanguageCode),
                Key: row.Field.ToLowerInvariant()))
            .FirstOrDefault(occupied.Contains);
        if (witnessOverlap != default)
            throw new InvalidDataException(
                $"A supplemental preserved witness overlaps a writable package: " +
                $"{witnessOverlap}.");
    }

    private async Task ValidateEntityTargetsAsync(LocalizationManifest manifest,
        ProductTagPackage productTags, ProductAttributePackage productAttributes,
        IList<LocalizedRow> localizedRows, IList<SlugRow> slugs,
        IList<RetiredUrlRecord> retiredUrlRecords)
    {
        await ValidateEntityIdsAsync(_blogPostRepository, IdsFor("BlogPost"), "BlogPost");
        await ValidateEntityIdsAsync(_categoryRepository, IdsFor("Category"), "Category");
        await ValidateEntityIdsAsync(_productRepository, IdsFor("Product"), "Product");
        await ValidateEntityIdsAsync(_productTagRepository, IdsFor("ProductTag"), "ProductTag");
        await ValidateEntityIdsAsync(_productAttributeRepository, IdsFor("ProductAttribute"), "ProductAttribute");
        await ValidateEntityIdsAsync(_productAttributeValueRepository, IdsFor("ProductAttributeValue"), "ProductAttributeValue");
        await ValidateEntityIdsAsync(_productAttributeMappingRepository, IdsFor("ProductAttributeMapping"), "ProductAttributeMapping");
        await ValidateEntityIdsAsync(_storeRepository, IdsFor("Store"), "Store");
        await ValidateEntityIdsAsync(_topicRepository, IdsFor("Topic"), "Topic");

        await ValidateManifestIdentityWitnessesAsync(manifest.Entries);
        await ValidateProductTagIdentityWitnessesAsync(productTags.Values);
        await ValidateProductAttributeIdentityWitnessesAsync(productAttributes.Rows);

        int[] IdsFor(string group) => localizedRows
            .Where(row => row.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.EntityId)
            .Distinct()
            .ToArray();

        // UrlRecord also acts as a durable identity witness for every desired
        // localized slug; an entity without its baseline URL is rejected.
        foreach (var group in slugs.GroupBy(row => row.EntityName, StringComparer.OrdinalIgnoreCase))
        {
            var ids = group.Select(row => row.EntityId).Distinct().ToArray();
            var existingIds = (await _urlRecordRepository.GetAllAsync(query => query.Where(record =>
                    record.EntityName == group.Key && ids.Contains(record.EntityId))))
                .Select(record => record.EntityId)
                .Distinct()
                .ToHashSet();
            var missing = ids.Where(id => !existingIds.Contains(id)).Take(10).ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException(
                    $"Localization package no longer matches {group.Key}; missing entity ids: {string.Join(',', missing)}");
        }

        await LoadAndValidateRetiredUrlRecordsAsync(retiredUrlRecords);
        await LoadAndValidateDisavowedForeignPreviousSlugRecordsAsync(
            manifest.DisavowedForeignPreviousSlugs);
    }

    private async Task ValidateManifestIdentityWitnessesAsync(IList<ManifestEntry> entries)
    {
        var witnesses = entries
            .GroupBy(entry => (entry.EntityType.ToLowerInvariant(), entry.EntityId))
            .Select(group =>
            {
                var witness = group.First();
                if (group.Any(entry => !entry.SourceHash.Equals(witness.SourceHash,
                                         StringComparison.OrdinalIgnoreCase) ||
                                       entry.Sku != witness.Sku || entry.SystemName != witness.SystemName ||
                                       entry.SourceLanguageId != witness.SourceLanguageId))
                    throw new InvalidDataException(
                        $"Localization package has inconsistent identity witnesses for {witness.EntityType}/{witness.EntityId}.");
                return witness;
            })
            .ToList();

        var categoryWitnesses = witnesses.Where(entry =>
            entry.EntityType.Equals(nameof(Category), StringComparison.OrdinalIgnoreCase)).ToList();
        var categorySlugs = await LoadInvariantSlugsAsync(nameof(Category),
            categoryWitnesses.Select(entry => entry.EntityId));
        var categories = await LoadEntitiesByIdAsync(_categoryRepository,
            categoryWitnesses.Select(entry => entry.EntityId));
        foreach (var witness in categoryWitnesses)
        {
            var category = categories[witness.EntityId];
            ValidateSourceHash(witness, category.Name, category.Description, category.MetaTitle,
                category.MetaDescription, category.MetaKeywords, categorySlugs[witness.EntityId]);
        }

        var topicWitnesses = witnesses.Where(entry =>
            entry.EntityType.Equals(nameof(Topic), StringComparison.OrdinalIgnoreCase)).ToList();
        var topicSlugs = await LoadInvariantSlugsAsync(nameof(Topic),
            topicWitnesses.Select(entry => entry.EntityId));
        var topics = await LoadEntitiesByIdAsync(_topicRepository,
            topicWitnesses.Select(entry => entry.EntityId));
        foreach (var witness in topicWitnesses)
        {
            var topic = topics[witness.EntityId];
            EnsureIdentityValue(nameof(Topic), topic.Id, nameof(Topic.SystemName), witness.SystemName,
                topic.SystemName);
            ValidateSourceHash(witness, topic.Title, topic.Body, topic.MetaTitle,
                topic.MetaDescription, topic.MetaKeywords, topicSlugs[witness.EntityId]);
        }

        var productWitnesses = witnesses.Where(entry =>
            entry.EntityType.Equals(nameof(Product), StringComparison.OrdinalIgnoreCase)).ToList();
        var products = await LoadEntitiesByIdAsync(_productRepository,
            productWitnesses.Select(entry => entry.EntityId));
        foreach (var witness in productWitnesses)
        {
            var product = products[witness.EntityId];
            ValidateProductIdentityWitness(witness, product);
        }

        var blogWitnesses = witnesses.Where(entry =>
            entry.EntityType.Equals(nameof(BlogPost), StringComparison.OrdinalIgnoreCase)).ToList();
        var blogPosts = await LoadEntitiesByIdAsync(_blogPostRepository,
            blogWitnesses.Select(entry => entry.EntityId));
        foreach (var witness in blogWitnesses)
        {
            var blogPost = blogPosts[witness.EntityId];
            if (witness.SourceLanguageId != blogPost.LanguageId)
                throw new InvalidDataException(
                    $"Localization package identity mismatch for BlogPost/{blogPost.Id}/LanguageId.");
            ValidateSourceHash(witness, blogPost.Title, blogPost.BodyOverview, blogPost.Body,
                blogPost.MetaTitle, blogPost.MetaDescription, blogPost.MetaKeywords, blogPost.Tags);
        }

        var storeWitnesses = witnesses.Where(entry =>
            entry.EntityType.Equals(nameof(Store), StringComparison.OrdinalIgnoreCase)).ToList();
        var stores = await LoadEntitiesByIdAsync(_storeRepository,
            storeWitnesses.Select(entry => entry.EntityId));
        foreach (var witness in storeWitnesses)
        {
            var store = stores[witness.EntityId];
            ValidateSourceHash(witness, store.Name, store.DefaultTitle, store.DefaultMetaDescription,
                store.DefaultMetaKeywords, store.HomepageTitle, store.HomepageDescription);
        }
    }

    private async Task ValidateProductTagIdentityWitnessesAsync(IList<ProductTagValue> values)
    {
        var witnesses = GetSourceWitnesses(values.Select(value =>
            (value.EntityId, Group: nameof(ProductTag), Key: nameof(ProductTag.Name), value.Source)));
        var tags = await LoadEntitiesByIdAsync(_productTagRepository, witnesses.Select(item => item.EntityId));
        foreach (var witness in witnesses)
            EnsureIdentityValue(nameof(ProductTag), witness.EntityId, nameof(ProductTag.Name), witness.Source,
                NormalizeSourceWitness(tags[witness.EntityId].Name));
    }

    private async Task ValidateProductAttributeIdentityWitnessesAsync(
        IList<ProductAttributeLocalizedValue> rows)
    {
        var witnesses = GetSourceWitnesses(rows.Select(row =>
            (row.EntityId, row.Group, row.Key, row.Source)));

        var attributes = await LoadEntitiesByIdAsync(_productAttributeRepository,
            witnesses.Where(item => item.Group.Equals(nameof(ProductAttribute), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EntityId));
        var values = await LoadEntitiesByIdAsync(_productAttributeValueRepository,
            witnesses.Where(item => item.Group.Equals(nameof(ProductAttributeValue), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EntityId));
        var mappings = await LoadEntitiesByIdAsync(_productAttributeMappingRepository,
            witnesses.Where(item => item.Group.Equals(nameof(ProductAttributeMapping), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EntityId));

        foreach (var witness in witnesses)
        {
            string current = witness.Group.ToLowerInvariant() switch
            {
                "productattribute" when witness.Key.Equals(nameof(ProductAttribute.Name), StringComparison.OrdinalIgnoreCase)
                    => attributes[witness.EntityId].Name,
                "productattribute" when witness.Key.Equals(nameof(ProductAttribute.Description), StringComparison.OrdinalIgnoreCase)
                    => attributes[witness.EntityId].Description,
                "productattributevalue" when witness.Key.Equals(nameof(ProductAttributeValue.Name), StringComparison.OrdinalIgnoreCase)
                    => values[witness.EntityId].Name,
                "productattributemapping" when witness.Key.Equals(nameof(ProductAttributeMapping.TextPrompt), StringComparison.OrdinalIgnoreCase)
                    => mappings[witness.EntityId].TextPrompt,
                "productattributemapping" when witness.Key.Equals(nameof(ProductAttributeMapping.DefaultValue), StringComparison.OrdinalIgnoreCase)
                    => mappings[witness.EntityId].DefaultValue,
                _ => throw new InvalidDataException(
                    $"Unsupported localization identity witness: {witness.Group}/{witness.Key}.")
            };
            EnsureIdentityValue(witness.Group, witness.EntityId, witness.Key, witness.Source,
                NormalizeSourceWitness(current));
        }
    }

    private static List<SourceWitness> GetSourceWitnesses(
        IEnumerable<(int EntityId, string Group, string Key, string Source)> rows)
    {
        return rows.GroupBy(row => (row.EntityId, Group: row.Group.ToLowerInvariant(),
                Key: row.Key.ToLowerInvariant()))
            .Select(group =>
            {
                var first = group.First();
                var sources = group.Select(row => row.Source).Distinct(StringComparer.Ordinal).ToArray();
                if (sources.Length != 1 || string.IsNullOrWhiteSpace(sources[0]))
                    throw new InvalidDataException(
                        $"Localization package has inconsistent source witnesses for {first.Group}/{first.EntityId}/{first.Key}.");
                return new SourceWitness(first.EntityId, first.Group, first.Key, sources[0]);
            })
            .ToList();
    }

    private async Task<Dictionary<int, string>> LoadInvariantSlugsAsync(string entityName,
        IEnumerable<int> entityIds)
    {
        var ids = entityIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<int, string>();

        var records = await _urlRecordRepository.GetAllAsync(query => query.Where(record =>
            record.EntityName == entityName && ids.Contains(record.EntityId) && record.LanguageId == 0 &&
            record.IsActive));
        var duplicate = records.GroupBy(record => record.EntityId).FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
            throw new InvalidDataException(
                $"Localization package identity witness found multiple invariant slugs for {entityName}/{duplicate.Key}.");
        var byId = records.ToDictionary(record => record.EntityId, record => record.Slug);
        var missing = ids.Where(id => !byId.ContainsKey(id)).Take(10).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                $"Localization package identity witness is missing invariant {entityName} slugs: {string.Join(',', missing)}");
        return byId;
    }

    private static async Task<Dictionary<int, TEntity>> LoadEntitiesByIdAsync<TEntity>(
        IRepository<TEntity> repository, IEnumerable<int> entityIds) where TEntity : BaseEntity
    {
        var ids = entityIds.Distinct().ToArray();
        var result = new Dictionary<int, TEntity>();
        foreach (var chunk in ids.Chunk(1000))
        {
            var chunkIds = chunk.ToArray();
            var entities = await repository.GetAllAsync(query => query.Where(entity => chunkIds.Contains(entity.Id)));
            foreach (var entity in entities)
                result.Add(entity.Id, entity);
        }

        var missing = ids.Where(id => !result.ContainsKey(id)).Take(10).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Localization package identity witness is missing entity ids: {string.Join(',', missing)}");
        return result;
    }

    internal static void ValidateSourceHash(ManifestEntry witness, params string[] sourceValues)
    {
        var actual = Sha256(string.Join('\0', sourceValues.Select(value => value ?? string.Empty)));
        if (!actual.Equals(witness.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Localization package source hash no longer matches {witness.EntityType}/{witness.EntityId}.");
    }

    internal static void ValidateProductIdentityWitness(ManifestEntry witness, Product product)
    {
        EnsureIdentityValue(nameof(Product), product.Id, nameof(Product.Sku), witness.Sku, product.Sku);
        ValidateSourceHash(witness, product.Name, product.ShortDescription, product.FullDescription,
            product.MetaTitle, product.MetaDescription, product.MetaKeywords);
    }

    private static void EnsureIdentityValue(string group, int entityId, string key,
        string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Localization package identity mismatch for {group}/{entityId}/{key}.");
    }

    private static string NormalizeSourceWitness(string value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[])null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static async Task ValidateEntityIdsAsync<TEntity>(IRepository<TEntity> repository,
        IReadOnlyCollection<int> expectedIds, string entityName) where TEntity : BaseEntity
    {
        if (expectedIds.Count == 0)
            return;

        var foundIds = new HashSet<int>();
        foreach (var chunk in expectedIds.Chunk(1000))
        {
            var ids = chunk.ToArray();
            var found = await repository.GetAllAsync(query => query.Where(entity => ids.Contains(entity.Id)));
            foundIds.UnionWith(found.Select(entity => entity.Id));
        }

        var missing = expectedIds.Where(id => !foundIds.Contains(id)).Take(10).ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                $"Localization package no longer matches {entityName}; missing entity ids: {string.Join(',', missing)}");
    }

    private async Task UpsertSlugsAsync(IList<SlugRow> rows, IList<RetiredUrlRecord> retiredUrlRecords)
    {
        var duplicateKey = rows.GroupBy(row => (row.EntityId, row.LanguageId,
                EntityName: row.EntityName.ToLowerInvariant()))
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicateKey is not null)
            throw new InvalidDataException($"Duplicate slug package key: {duplicateKey.Key}");
        var duplicateSlug = rows.GroupBy(row => row.Slug, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Select(row => (row.EntityName.ToLowerInvariant(), row.EntityId))
                .Distinct().Count() > 1);
        if (duplicateSlug is not null)
            throw new InvalidDataException($"Slug belongs to multiple entities: {duplicateSlug.Key}");
        var previousSlugOwners = rows
            .SelectMany(row => row.PreviousSlugs.Select(slug => new
            {
                Slug = slug,
                Owner = (EntityName: row.EntityName.ToLowerInvariant(), row.EntityId)
            }))
            .GroupBy(item => item.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Owner).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var ambiguousPreviousSlug = previousSlugOwners.FirstOrDefault(pair => pair.Value.Length > 1);
        if (!string.IsNullOrEmpty(ambiguousPreviousSlug.Key))
            throw new InvalidDataException(
                $"Previous slug redirect belongs to multiple entities: {ambiguousPreviousSlug.Key}");

        var reviewedTransfers = await LoadAndValidateRetiredUrlRecordsAsync(retiredUrlRecords);
        var reviewedTransferIds = reviewedTransfers.Select(record => record.Id).ToHashSet();
        var allUrlRecords = await _urlRecordRepository.GetAllAsync(
            (IQueryable<UrlRecord> query) => query);
        ValidateDesiredSlugOwnership(rows, allUrlRecords, reviewedTransferIds);
        var activeRecords = allUrlRecords.Where(item => item.IsActive).ToList();
        var desiredOwners = rows.GroupBy(row => row.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return (EntityName: row.EntityName.ToLowerInvariant(), row.EntityId);
                },
                StringComparer.OrdinalIgnoreCase);
        foreach (var (previousSlug, owners) in previousSlugOwners)
        {
            if (desiredOwners.TryGetValue(previousSlug, out var desiredOwner) && desiredOwner != owners[0])
                throw new InvalidDataException(
                    $"Previous slug redirect '{previousSlug}' is now assigned to another entity.");
            var unknownHistoricalOwner = allUrlRecords.FirstOrDefault(current =>
                current.Slug.Equals(previousSlug, StringComparison.OrdinalIgnoreCase) &&
                (current.EntityName.ToLowerInvariant(), current.EntityId) != owners[0]);
            if (unknownHistoricalOwner is not null)
                throw new InvalidDataException(
                    $"Previous slug redirect '{previousSlug}' belongs to another entity.");
        }
        foreach (var current in activeRecords)
        {
            if (previousSlugOwners.TryGetValue(current.Slug, out var previousOwners) &&
                previousOwners[0] != (current.EntityName.ToLowerInvariant(), current.EntityId))
                throw new InvalidDataException(
                    $"Previous slug redirect '{current.Slug}' is active for another entity.");
        }

        var recordsToDeactivate = reviewedTransfers.Where(record => record.IsActive).ToList();
        foreach (var current in recordsToDeactivate)
            current.IsActive = false;
        var recordsToActivate = new List<UrlRecord>();
        var recordsToInsert = new List<UrlRecord>();
        foreach (var group in rows.GroupBy(row => row.EntityName, StringComparer.OrdinalIgnoreCase))
        {
            var entityIds = group.Select(row => row.EntityId).Distinct().ToArray();
            var languageIds = group.Select(row => row.LanguageId).Distinct().ToArray();
            var existing = await _urlRecordRepository.GetAllAsync(query => query.Where(item =>
                item.EntityName == group.Key && entityIds.Contains(item.EntityId) &&
                languageIds.Contains(item.LanguageId)));

            foreach (var row in group)
            {
                var matches = existing.Where(item => item.EntityId == row.EntityId &&
                    item.LanguageId == row.LanguageId).ToList();
                var desired = matches.FirstOrDefault(item =>
                    item.Slug.Equals(row.Slug, StringComparison.OrdinalIgnoreCase));
                var deactivations = matches.Where(item => item.IsActive && item != desired).ToList();
                foreach (var current in deactivations)
                    current.IsActive = false;
                recordsToDeactivate.AddRange(deactivations);

                if (desired is null)
                {
                    recordsToInsert.Add(new UrlRecord
                    {
                        EntityId = row.EntityId,
                        EntityName = row.EntityName,
                        LanguageId = row.LanguageId,
                        Slug = row.Slug,
                        IsActive = true
                    });
                }
                else if (!desired.IsActive)
                {
                    desired.IsActive = true;
                    recordsToActivate.Add(desired);
                }

                foreach (var previousSlug in row.PreviousSlugs)
                {
                    if (matches.Any(item => item.Slug.Equals(previousSlug,
                            StringComparison.OrdinalIgnoreCase)))
                        continue;
                    recordsToInsert.Add(new UrlRecord
                    {
                        EntityId = row.EntityId,
                        EntityName = row.EntityName,
                        LanguageId = row.LanguageId,
                        Slug = previousSlug,
                        IsActive = false
                    });
                }
            }
        }

        if (recordsToDeactivate.Count > 0)
            await _urlRecordRepository.UpdateAsync(recordsToDeactivate, false);
        if (recordsToActivate.Count > 0)
            await _urlRecordRepository.UpdateAsync(recordsToActivate, false);
        if (recordsToInsert.Count > 0)
            await _urlRecordRepository.InsertAsync(recordsToInsert, false);
    }

    internal static void ValidateDesiredSlugOwnership(IList<SlugRow> rows,
        IList<UrlRecord> allUrlRecords, ISet<int> reviewedTransferIds)
    {
        var desiredOwners = rows.GroupBy(row => row.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var row = group.First();
                    return (EntityName: row.EntityName.ToLowerInvariant(), row.EntityId);
                },
                StringComparer.OrdinalIgnoreCase);

        foreach (var current in allUrlRecords)
        {
            if (!desiredOwners.TryGetValue(current.Slug, out var desiredOwner) ||
                desiredOwner == (current.EntityName.ToLowerInvariant(), current.EntityId))
                continue;
            if (!reviewedTransferIds.Contains(current.Id))
                throw new InvalidDataException(
                    $"Desired slug '{current.Slug}' has an unreviewed historical owner {current.EntityName}/{current.EntityId} (UrlRecord {current.Id}).");
        }
    }

    private async Task<IList<UrlRecord>> LoadAndValidateRetiredUrlRecordsAsync(
        IList<RetiredUrlRecord> retiredUrlRecords)
    {
        if (retiredUrlRecords.Count == 0)
            return Array.Empty<UrlRecord>();

        var ids = retiredUrlRecords.Select(record => record.Id).ToArray();
        var currentRecords = await _urlRecordRepository.GetAllAsync(query => query.Where(record => ids.Contains(record.Id)));
        return ValidateRetiredUrlRecords(retiredUrlRecords, currentRecords);
    }

    internal static IList<UrlRecord> ValidateRetiredUrlRecords(IList<RetiredUrlRecord> retiredUrlRecords,
        IList<UrlRecord> currentRecords)
    {
        if (retiredUrlRecords.Select(record => record.Id).Distinct().Count() != retiredUrlRecords.Count)
            throw new InvalidDataException("Reviewed retired URL records contain duplicate ids.");
        var currentById = currentRecords.ToDictionary(record => record.Id);
        foreach (var reviewed in retiredUrlRecords)
        {
            if (!currentById.TryGetValue(reviewed.Id, out var current) ||
                current.EntityId != reviewed.EntityId || current.LanguageId != reviewed.LanguageId ||
                !current.EntityName.Equals(reviewed.EntityName, StringComparison.Ordinal) ||
                !current.Slug.Equals(reviewed.Slug, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Reviewed retired URL record no longer matches the catalog: {reviewed.Id}.");
        }
        return currentRecords;
    }

    private async Task LoadAndValidateDisavowedForeignPreviousSlugRecordsAsync(
        IList<DisavowedForeignPreviousSlug> evidenceRows)
    {
        var ids = evidenceRows
            .SelectMany(evidence => evidence.SameOwnerUrlRecordIds
                .Concat(evidence.ForeignOwners.Select(owner => owner.UrlRecordId)))
            .Distinct()
            .ToArray();
        var currentRecords = await _urlRecordRepository.GetAllAsync(query =>
            query.Where(record => ids.Contains(record.Id)));
        ValidateDisavowedForeignPreviousSlugRecords(evidenceRows, currentRecords);
    }

    internal static void ValidateDisavowedForeignPreviousSlugRecords(
        IList<DisavowedForeignPreviousSlug> evidenceRows, IList<UrlRecord> currentRecords)
    {
        var expectedIds = evidenceRows
            .SelectMany(evidence => evidence.SameOwnerUrlRecordIds
                .Concat(evidence.ForeignOwners.Select(owner => owner.UrlRecordId)))
            .ToList();
        if (expectedIds.Count == 0 || expectedIds.Any(id => id <= 0) ||
            expectedIds.Distinct().Count() != expectedIds.Count || currentRecords is null ||
            currentRecords.Select(record => record.Id).Distinct().Count() != currentRecords.Count ||
            currentRecords.Count != expectedIds.Count)
            throw new InvalidDataException(
                "Disavowed foreign previous-slug URL-record witnesses no longer match the catalog.");

        var currentById = currentRecords.ToDictionary(record => record.Id);
        foreach (var evidence in evidenceRows)
        {
            foreach (var sameOwnerId in evidence.SameOwnerUrlRecordIds)
            {
                if (!currentById.TryGetValue(sameOwnerId, out var current) ||
                    !MatchesUrlRecordIdentity(current, evidence.EntityType, evidence.EntityId,
                        evidence.LanguageId, evidence.Slug))
                    throw new InvalidDataException(
                        $"Disavowed previous-slug same-owner witness no longer matches the catalog: {sameOwnerId}.");
            }

            foreach (var foreignOwner in evidence.ForeignOwners)
            {
                if (!foreignOwner.IsActive.HasValue ||
                    !currentById.TryGetValue(foreignOwner.UrlRecordId, out var current) ||
                    !MatchesUrlRecordIdentity(current, foreignOwner.EntityType, foreignOwner.EntityId,
                        foreignOwner.LanguageId, evidence.Slug) || current.IsActive != foreignOwner.IsActive.Value)
                    throw new InvalidDataException(
                        $"Disavowed previous-slug foreign-owner witness no longer matches the catalog: {foreignOwner.UrlRecordId}.");
            }
        }
    }

    private static bool MatchesUrlRecordIdentity(UrlRecord current, string entityName, int entityId,
        int languageId, string slug) =>
        current.EntityId == entityId && current.LanguageId == languageId &&
        string.Equals(current.EntityName, entityName, StringComparison.Ordinal) &&
        string.Equals(current.Slug, slug, StringComparison.Ordinal);

    private static T ReadEmbedded<T>(string fileName)
    {
        var payload = ReadEmbeddedBytes(fileName);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException($"Embedded localization resource is blank: {ResourcePrefix + fileName}");
    }

    private static byte[] ReadEmbeddedBytes(string fileName)
    {
        var assembly = typeof(LocalizationResourceInstaller).Assembly;
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded localization resource not found: {resourceName}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void ValidateQualityGate(LocalizationQualityGate qualityGate)
    {
        var expectedFiles = new[]
        {
            "localization-manifest.json",
            "ui-localization-resources.json",
            "product-tag-localization.json",
            "product-attribute-localization.json",
            "product-prose-koc-corrections.json",
            "product-prose-supplemental-corrections.json"
        };
        var expectedReviewFiles = new[]
        {
            "final-repetition-review-west.json",
            "final-repetition-review-north.json",
            "final-repetition-review-global.json",
            "independent-north-qa.json",
            "independent-west-qa.json",
            "independent-global-qa.json",
            "final-pathology-supplemental-review.json",
            "independent-final-pathology-supplemental-qa.json"
        };
        ValidateFinalEvidenceContract(qualityGate, MatchesEmbeddedSha256);
        ValidateProductProseLinguisticQualityGateContract(qualityGate, MatchesEmbeddedSha256);
        ValidateProductProseSupplementalSlugQualityGateContract(qualityGate);
        if (qualityGate.SchemaVersion != 7 || qualityGate.SlugPolicyVersion != 2 ||
            qualityGate.SlugRegenerationErrorCount != 0 || qualityGate.NumericOnlySlugCount != 0 ||
            qualityGate.ManifestLocalizedEntryCount != 1168 ||
            qualityGate.ManifestSlugValueLinkCheckCount != 1120 ||
            qualityGate.ProductTagSlugValueLinkCheckCount != 819 * 24 ||
            qualityGate.SlugValueLinkCheckCount != qualityGate.ManifestSlugValueLinkCheckCount +
                qualityGate.ProductTagSlugValueLinkCheckCount ||
            qualityGate.ManifestPreviousSlugCount != 107 ||
            qualityGate.ProductTagPreviousSlugCount != 7035 ||
            qualityGate.RetainedPreviousSlugCount != 7142 ||
            qualityGate.DisavowedForeignPreviousSlugCount != 20 ||
            qualityGate.LanguageCount != 24 ||
            qualityGate.PathologyErrorCount != 0 || qualityGate.ProductQualityErrorCount != 0 ||
            qualityGate.ProductTagReviewedTagCount != 819 ||
            qualityGate.ProductTagLocalizedValueCount != 819 * 24 ||
            qualityGate.BlogTagReviewedResourceCount != 134 ||
            qualityGate.BlogTagReviewedTranslationCount != 134 * 19 ||
            qualityGate.PublicUiResourceCountPerLanguage != 211 ||
            qualityGate.PublicUiLocalizedEntryCount != 211 * 24 ||
            qualityGate.ContactUiReviewedResourceCount != 15 ||
            qualityGate.ContactUiReviewedTranslationCount != 15 * 19 ||
            qualityGate.AttributeDistinctSourceCount != 432 ||
            qualityGate.AttributeLocalizedRowCount != 106176 ||
            qualityGate.KocNockCheckCount != 1224 ||
            qualityGate.ProductProseKocCorrectionCount != 2849 ||
            qualityGate.ProductProseKocCorrectionOccurrenceCount != 2849 ||
            qualityGate.ProductProseKocCorrectionProductCount != 63 ||
            qualityGate.ProductProseKocCorrectionLanguageCount != 24 ||
            qualityGate.ProductProseKocHtmlStructureCheckCount != 2849 ||
            qualityGate.ProductProseKocCorrectionErrorCount != 0 ||
            qualityGate.ProductProseSupplementalCorrectionCount != 44 ||
            qualityGate.ProductProseSupplementalProductCount != 36 ||
            qualityGate.ProductProseSupplementalLanguageCount != 1 ||
            qualityGate.ProductProseSupplementalNameCount != 8 ||
            qualityGate.ProductProseSupplementalShortDescriptionCount != 6 ||
            qualityGate.ProductProseSupplementalFullDescriptionCount != 30 ||
            qualityGate.ProductProseSupplementalEditCount != 65 ||
            qualityGate.ProductProseSupplementalExactEditRowCount != 43 ||
            qualityGate.ProductProseSupplementalFullReplacementCount != 1 ||
            qualityGate.ProductProseSupplementalHtmlStructureCheckCount != 44 ||
            qualityGate.ProductProseSupplementalHtmlStructureChangeCount != 1 ||
            qualityGate.ProductProseSupplementalWitnessEntryCount != 9 ||
            qualityGate.ProductProseSupplementalWitnessUniqueTupleCount != 7 ||
            qualityGate.ProductProseSupplementalWitnessPhraseOccurrenceCount != 21 ||
            qualityGate.ProductProseSupplementalCompoundWitnessCount != 6 ||
            qualityGate.ProductProseSupplementalSwordWitnessCount != 3 ||
            qualityGate.ProductProseSupplementalStandaloneUrduNoseTargetCount != 15 ||
            qualityGate.ProductProseSupplementalIncorrectUrduNockTermTargetCount != 31 ||
            qualityGate.ProductProseSupplementalIssueSetOverlapCount != 2 ||
            !string.Equals(qualityGate.ProductProseSupplementalTargetTupleSetSha256,
                "595AAF6E4E270445AE7B3376B2B7B1CE457C13A957568A344E1398230F7A3B85",
                StringComparison.OrdinalIgnoreCase) ||
            qualityGate.ProductProseSupplementalSemanticResidualCount != 0 ||
            qualityGate.ProductProseSupplementalErrorCount != 0 ||
            qualityGate.ProductProseAcceptedPreviousValueCount != 1 ||
            qualityGate.ReviewedProductProseTranslationCount != 114 ||
            !MatchesEmbeddedSha256("reviewed-prose-quality-audit.json",
                qualityGate.ReviewedProseQualityAuditSha256) ||
            qualityGate.ReviewedArcheryCategoryTranslationCount != 95 ||
            qualityGate.ReviewedTurkishBowSourceCount != 16 ||
            qualityGate.SemanticReviewedCellApplicationCount != 11921 ||
            qualityGate.ManifestContentQualityErrorCount != 0 ||
            !MatchesEmbeddedSha256("archery-tag-category-manual-quality-audit.json",
                qualityGate.ReviewedArcheryCategoryAuditSha256) ||
            !MatchesEmbeddedSha256("manifest-content-quality-audit.json",
                qualityGate.ManifestContentQualityAuditSha256) ||
            !MatchesEmbeddedSha256("semantic-review-application-quality-audit.json",
                qualityGate.SemanticReviewApplicationAuditSha256) ||
            qualityGate.DesiredSlugOwnerConflictCount != 0 ||
            qualityGate.ReviewedUrlTransferCount != 1 ||
            qualityGate.ManualProductPairReviewCount != 7506 + 8757 + 7506 ||
            qualityGate.ManualContactUiReviewCount != 90 + 105 + 90 ||
            qualityGate.IndependentReviewCorrectionCount != 58 ||
            qualityGate.SupplementalPathologyReviewCount != 16 ||
            qualityGate.SupplementalIndependentReviewCount != 16 ||
            qualityGate.ManualReviewPrimaryFixCount != 1365 ||
            qualityGate.ManualReviewApplicationErrorCount != 0 ||
            qualityGate.ManualReviewAppliedTargetCount != 1363 ||
            qualityGate.ReviewReportSha256.Count != expectedReviewFiles.Length ||
            expectedReviewFiles.Any(fileName =>
                !qualityGate.ReviewReportSha256.TryGetValue(fileName, out var expectedReviewHash) ||
                string.IsNullOrWhiteSpace(expectedReviewHash) || expectedReviewHash.Length != 64 ||
                !Convert.ToHexString(SHA256.HashData(ReadEmbeddedBytes(fileName)))
                    .Equals(expectedReviewHash, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(qualityGate.ManualReviewApplicationSha256) ||
            qualityGate.ManualReviewApplicationSha256.Length != 64 ||
            !Convert.ToHexString(SHA256.HashData(ReadEmbeddedBytes("manual-review-application-audit.json")))
                .Equals(qualityGate.ManualReviewApplicationSha256, StringComparison.OrdinalIgnoreCase) ||
            qualityGate.PackageSha256.Count != expectedFiles.Length ||
            expectedFiles.Any(fileName => !qualityGate.PackageSha256.TryGetValue(fileName, out var expectedHash) ||
                string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64 ||
                !Convert.ToHexString(SHA256.HashData(ReadEmbeddedBytes(fileName)))
                    .Equals(expectedHash, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Embedded localization quality gate did not pass.");
    }

    internal static void ValidateFinalEvidenceContract(LocalizationQualityGate qualityGate,
        Func<string, string, bool> matchesEmbeddedSha256)
    {
        var expectedEvidenceFiles = new[]
        {
            "final-manifest-independent-qa-v3.json",
            "product-candidate-independent-qa-v4.json",
            "unchanged-label-independent-qa-v2.json",
            "final-manifest-resolution-audit.json",
            "manifest-content-quality-audit.json",
            "product-localization-quality-audit.json",
            "slug-regeneration-audit.json",
            "translation-pathology-audit.json",
            "larp-slug-linguistic-independent-qa.json"
        };
        if (qualityGate.SchemaVersion != 7 ||
            !qualityGate.FinalManifestIndependentQaDeployable ||
            !qualityGate.FinalProductIndependentQaDeployable ||
            !qualityGate.FinalUnchangedLabelIndependentQaDeployable ||
            !qualityGate.FinalLarpSlugLinguisticIndependentQaDeployable ||
            qualityGate.FinalManifestResolvedTupleCount != 357 ||
            qualityGate.PublicMetaDescriptionCompletenessCheckCount != 479 ||
            qualityGate.FinalProductCorrectionCheckCount != 43 ||
            qualityGate.FinalProductLabelCorrectionCheckCount != 203 ||
            qualityGate.FinalProductLabelCorrectedRowCount != 635 ||
            qualityGate.FinalProductTagCorrectedRowCount != 119 ||
            qualityGate.FinalProductAttributeCorrectionGroupCount != 84 ||
            qualityGate.FinalProductAttributeCorrectedRowCount != 516 ||
            qualityGate.FinalAcceptedLabelReviewedWarningRowCount != 6804 ||
            qualityGate.FinalProductRemainingReviewedWarningRowCount != 6778 ||
            qualityGate.FinalProductLabelCorrectionCheckCount !=
                qualityGate.FinalProductTagCorrectedRowCount +
                qualityGate.FinalProductAttributeCorrectionGroupCount ||
            qualityGate.FinalProductLabelCorrectedRowCount !=
                qualityGate.FinalProductTagCorrectedRowCount +
                qualityGate.FinalProductAttributeCorrectedRowCount ||
            qualityGate.FinalProductLabelCorrectedRowCount +
                qualityGate.FinalProductRemainingReviewedWarningRowCount != 7413 ||
            qualityGate.KocNockCheckCount != 1224 ||
            qualityGate.OpaqueTypeCodeCheckCount != 4248 ||
            qualityGate.ManifestPreviousSlugCount != 107 ||
            qualityGate.ProductTagPreviousSlugCount != 7035 ||
            qualityGate.RetainedPreviousSlugCount != 7142 ||
            qualityGate.DisavowedForeignPreviousSlugCount != 20 ||
            qualityGate.ReviewedLarpSlugMappingCount != 63 ||
            qualityGate.NumericLarpCollisionCounterSlugCount != 0 ||
            qualityGate.LiveUrlRecordOwnerConflictCount != 0 ||
            qualityGate.DisavowedForeignProductTagHistoryCount != 1 ||
            !IsSha256(qualityGate.LiveUrlRecordOwnershipIndependentQaSha256) ||
            !matchesEmbeddedSha256("live-urlrecord-ownership-independent-qa.json",
                qualityGate.LiveUrlRecordOwnershipIndependentQaSha256) ||
            qualityGate.FinalEvidenceReportSha256 is null ||
            qualityGate.FinalEvidenceReportSha256.Count != expectedEvidenceFiles.Length ||
            expectedEvidenceFiles.Any(fileName =>
                !qualityGate.FinalEvidenceReportSha256.TryGetValue(fileName, out var expectedHash) ||
                !IsSha256(expectedHash) || !matchesEmbeddedSha256(fileName, expectedHash)))
            throw new InvalidDataException("Embedded localization quality gate did not pass.");
    }

    internal static void ValidateProductProseLinguisticQualityGateContract(
        LocalizationQualityGate qualityGate, Func<string, string, bool> matchesEmbeddedSha256)
    {
        ArgumentNullException.ThrowIfNull(qualityGate);
        ArgumentNullException.ThrowIfNull(matchesEmbeddedSha256);
        if (qualityGate.SchemaVersion != 7 || qualityGate.ProductProseAdditionalEditCount != 3 ||
            qualityGate.ProductProseAdditionalEditRowCount != 2 ||
            !qualityGate.FinalProductProseLinguisticIndependentQaDeployable ||
            qualityGate.ProductProseLinguisticRouteCount != 24 ||
            qualityGate.ProductProseLinguisticDistinctSentenceCount != 20 ||
            qualityGate.ProductProseLinguisticAuthorityReviewCount != 20 ||
            qualityGate.ProductProseLinguisticSentenceReviewCount != 20 ||
            qualityGate.ProductProseLinguisticErrorCount != 0 ||
            qualityGate.UrduNockLinguisticReviewedRowCount != 258 ||
            qualityGate.UrduNockLinguisticProductTagReviewedRowCount != 8 ||
            qualityGate.UrduNockLinguisticProductAttributeReviewedRowCount != 250 ||
            qualityGate.UrduNockLinguisticResidualCount != 0 ||
            !IsSha256(qualityGate.ProductProseCanonicalLinguisticIndependentQaSha256) ||
            !matchesEmbeddedSha256("product-prose-canonical-linguistic-independent-qa.json",
                qualityGate.ProductProseCanonicalLinguisticIndependentQaSha256) ||
            !IsSha256(qualityGate.UrduNockLinguisticCorrectionAuditSha256) ||
            !matchesEmbeddedSha256("urdu-nock-linguistic-correction-audit.json",
                qualityGate.UrduNockLinguisticCorrectionAuditSha256))
            throw new InvalidDataException(
                "Embedded product-prose linguistic quality gate did not pass.");
    }

    internal static void ValidateProductProseSupplementalSlugQualityGateContract(
        LocalizationQualityGate qualityGate)
    {
        ArgumentNullException.ThrowIfNull(qualityGate);
        if (qualityGate.SchemaVersion != 7 ||
            qualityGate.ProductProseSupplementalSlugPolicyVersion != 2 ||
            qualityGate.ProductProseSupplementalReviewedSlugCount != 8 ||
            qualityGate.ProductProseSupplementalSlugSourceNameLinkCount != 8 ||
            qualityGate.ProductProseSupplementalPreviousSlugRedirectCount != 8 ||
            qualityGate.ProductProseSupplementalUniqueSlugCount != 8 ||
            qualityGate.ProductProseSupplementalUniquePreviousSlugCount != 8 ||
            qualityGate.ProductProseSupplementalProhibitedSlugTermCount != 0 ||
            qualityGate.ProductProseSupplementalSlugErrorCount != 0 ||
            !string.Equals(qualityGate.ProductProseSupplementalSlugTargetSetSha256,
                "70CE09B77B5AB4E42DBFBBE4E1C7494CC5C778665AA89DE748339E30422FEAC2",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Embedded supplemental product-slug quality gate did not pass.");
    }

    private static bool MatchesEmbeddedSha256(string fileName, string expectedHash) =>
        !string.IsNullOrWhiteSpace(expectedHash) && expectedHash.Length == 64 &&
        Convert.ToHexString(SHA256.HashData(ReadEmbeddedBytes(fileName)))
            .Equals(expectedHash, StringComparison.OrdinalIgnoreCase);

    private static void ValidatePackage(LocalizationQualityGate qualityGate, LocalizationManifest manifest,
        UiResourcePackage uiResources, ProductTagPackage productTags, ProductAttributePackage productAttributes)
    {
        ValidateCoverageContract(qualityGate.ManifestLocalizedEntryCount,
            qualityGate.ManifestSlugValueLinkCheckCount, qualityGate.ProductTagSlugValueLinkCheckCount,
            qualityGate.SlugValueLinkCheckCount, manifest.Entries.Count,
            manifest.Entries.Count(entry => entry.Slug is not null), productTags.Slugs.Count,
            productAttributes.SchemaVersion);
        ValidateRetiredUrlRecordPackageContract(manifest.RetiredUrlRecordIds,
            manifest.RetiredUrlRecords);

        var allowedManifestGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "BlogPost", "Category", "Product", "Store", "Topic" };
        var optionalBlankManifestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "MetaKeywords", "MetaDescription", "BodyOverview" };
        var manifestPreviousSlugCount = manifest.Entries.Sum(entry => entry.PreviousSlugs.Count);
        var productTagPreviousSlugCount = productTags.Slugs.Sum(entry => entry.PreviousSlugs.Count);
        if (manifest.SchemaVersion != 2 || manifest.SlugPolicyVersion != 2 ||
            manifest.Validation.ErrorCount != 0 ||
            qualityGate.ManifestPreviousSlugCount != manifestPreviousSlugCount ||
            qualityGate.ProductTagPreviousSlugCount != productTagPreviousSlugCount ||
            qualityGate.RetainedPreviousSlugCount != manifestPreviousSlugCount + productTagPreviousSlugCount ||
            qualityGate.DisavowedForeignPreviousSlugCount != manifest.DisavowedForeignPreviousSlugCount ||
            manifest.RetiredUrlRecordIds.Count != 1 || manifest.RetiredUrlRecords.Count != 1 ||
            !manifest.RetiredUrlRecordIds.SequenceEqual(new[] { 708 }) ||
            !manifest.RetiredUrlRecordIds.OrderBy(id => id)
                .SequenceEqual(manifest.RetiredUrlRecords.Select(record => record.Id).OrderBy(id => id)) ||
            manifest.RetiredUrlRecords.Any(record => record.Id <= 0 || record.EntityId <= 0 ||
                record.LanguageId < 0 || string.IsNullOrWhiteSpace(record.EntityName) ||
                string.IsNullOrWhiteSpace(record.Slug) || record.Slug.Length > 200) ||
            manifest.Entries.Any(entry => entry.EntityId <= 0 ||
                string.IsNullOrWhiteSpace(entry.LanguageCode) ||
                !allowedManifestGroups.Contains(entry.EntityType) || entry.Fields.Count == 0 ||
                !IsSha256(entry.SourceHash) ||
                entry.EntityType.Equals(nameof(Product), StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(entry.Sku) ||
                entry.EntityType.Equals(nameof(Topic), StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(entry.SystemName) ||
                entry.EntityType.Equals(nameof(BlogPost), StringComparison.OrdinalIgnoreCase) &&
                    entry.SourceLanguageId <= 0 ||
                entry.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || field.Value is null ||
                    string.IsNullOrWhiteSpace(field.Value) && !optionalBlankManifestKeys.Contains(field.Key)) ||
                entry.Slug is not null && (string.IsNullOrWhiteSpace(entry.Slug) || entry.Slug.Length > 200 ||
                    entry.SlugPolicyVersion != 2 || string.IsNullOrWhiteSpace(entry.SlugPolicy) ||
                    string.IsNullOrWhiteSpace(entry.SlugSource) || string.IsNullOrWhiteSpace(entry.SlugBase) ||
                    !IsSha256(entry.SlugSourceSha256) ||
                    !Sha256(entry.SlugSource).Equals(entry.SlugSourceSha256,
                        StringComparison.OrdinalIgnoreCase) || !IsSemanticSlug(entry.Slug) ||
                    !IsSemanticSlug(entry.SlugBase) || !IsReservedSlugVariant(entry.Slug, entry.SlugBase) ||
                    entry.PreviousSlugs.Any(previous => string.IsNullOrWhiteSpace(previous) ||
                        previous.Equals(entry.Slug, StringComparison.OrdinalIgnoreCase)) ||
                    entry.PreviousSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                    entry.PreviousSlugs.Count)))
            throw new InvalidDataException("Core localization manifest did not pass its embedded validation gate.");
        if (uiResources.LanguageCount != 24 || uiResources.ResourceCountPerLanguage != 211 ||
            uiResources.Entries.Count != 5064 ||
            uiResources.Entries.Any(entry => string.IsNullOrWhiteSpace(entry.LanguageCode) ||
                string.IsNullOrWhiteSpace(entry.ResourceName) || string.IsNullOrWhiteSpace(entry.ResourceValue)))
            throw new InvalidDataException("UI resource package coverage is incomplete.");
        var invalidProductTagValueCount = productTags.Values.Count(entry => entry.EntityId <= 0 ||
            string.IsNullOrWhiteSpace(entry.LanguageCode) || string.IsNullOrWhiteSpace(entry.Source) ||
            string.IsNullOrWhiteSpace(entry.Value));
        var invalidProductTagSlugCount = productTags.Slugs.Count(entry => entry.EntityId <= 0 ||
            string.IsNullOrWhiteSpace(entry.LanguageCode) || string.IsNullOrWhiteSpace(entry.Slug) ||
            entry.Slug.Length > 200 || entry.SlugPolicyVersion != 2 || !IsSha256(entry.ValueSha256) ||
            string.IsNullOrWhiteSpace(entry.SlugBase) || !IsSemanticSlug(entry.Slug) ||
            !IsSemanticSlug(entry.SlugBase) || !IsReservedSlugVariant(entry.Slug, entry.SlugBase) ||
            entry.PreviousSlugs.Any(previous => string.IsNullOrWhiteSpace(previous) ||
                previous.Equals(entry.Slug, StringComparison.OrdinalIgnoreCase)) ||
            entry.PreviousSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            entry.PreviousSlugs.Count);
        if (productTags.SchemaVersion != 2 || productTags.SlugPolicyVersion != 2 ||
            productTags.ValueSlugRegenerationCheckCount != 819 * 24 ||
            productTags.NumericOnlySlugCount != 0 || productTags.RetainedPreviousSlugCount != 7035 ||
            productTags.TagCount != 819 || productTags.LanguageCount != 24 ||
            productTags.Values.Count != 819 * 24 || productTags.Slugs.Count != 819 * 24 ||
            productTags.Audit.ErrorCount != 0 || invalidProductTagValueCount != 0 ||
            invalidProductTagSlugCount != 0)
            throw new InvalidDataException(
                $"Product-tag localization package coverage is incomplete (invalid values: " +
                $"{invalidProductTagValueCount}; invalid slugs: {invalidProductTagSlugCount}).");
        ValidateDisavowedForeignPreviousSlugs(manifest, productTags);
        var productTagValuesByKey = productTags.Values
            .GroupBy(entry => (entry.EntityId, entry.LanguageId))
            .ToDictionary(group => group.Key, group => group.ToList());
        if (productTagValuesByKey.Count != productTags.Values.Count ||
            productTags.Slugs.Any(entry =>
                !productTagValuesByKey.TryGetValue((entry.EntityId, entry.LanguageId), out var values) ||
                values.Count != 1 || !Sha256(values[0].Value).Equals(entry.ValueSha256,
                    StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Product-tag localized values and SEO slugs are not linked.");
        var allowedAttributeKeys = new HashSet<(string Group, string Key)>(new LocalizedGroupKeyComparer())
        {
            ("ProductAttribute", "Name"),
            ("ProductAttribute", "Description"),
            ("ProductAttributeValue", "Name"),
            ("ProductAttributeMapping", "TextPrompt"),
            ("ProductAttributeMapping", "DefaultValue")
        };
        if (productAttributes.LanguageCount != 24 || productAttributes.Audit.ErrorCount != 0 ||
            productAttributes.AttributeCount != 46 || productAttributes.AttributeValueCount != 4359 ||
            productAttributes.MappingCount != 12 || productAttributes.LocalizedValueCount != 106176 ||
            productAttributes.Rows.Count != productAttributes.LocalizedValueCount ||
            productAttributes.Rows.Any(entry => entry.EntityId <= 0 ||
                string.IsNullOrWhiteSpace(entry.LanguageCode) ||
                string.IsNullOrWhiteSpace(entry.Group) || string.IsNullOrWhiteSpace(entry.Key) ||
                string.IsNullOrWhiteSpace(entry.Source) ||
                !allowedAttributeKeys.Contains((entry.Group, entry.Key)) || string.IsNullOrWhiteSpace(entry.Value)))
            throw new InvalidDataException("Product-attribute localization package coverage is incomplete.");
    }

    internal static void ValidateProductProseCanonicalLinguisticQa(
        ProductProseCanonicalLinguisticQa qa, ProductProseKocPackage productProse,
        ProductTagPackage productTags, Func<string, string, bool> matchesEmbeddedSha256)
    {
        ArgumentNullException.ThrowIfNull(qa);
        ArgumentNullException.ThrowIfNull(productProse);
        ArgumentNullException.ThrowIfNull(productTags);
        ArgumentNullException.ThrowIfNull(matchesEmbeddedSha256);

        var expectedRoutes = new HashSet<string>(StringComparer.Ordinal)
        {
            "ar", "au", "ca", "de", "dk", "en", "es", "fr", "gb", "gr", "hu", "it",
            "jp", "my", "nl", "no", "pl", "pt", "ro", "ru", "se", "tr", "ur", "za"
        };
        var expectedCheckNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "authorityTermIsArcherySpecific", "brandKoçPreserved", "animalSenseAbsent",
            "nonArcherySenseAbsent", "standardAlternativeClear", "nativeGrammarNatural",
            "customerInstructionNatural", "noUnreviewedEnglishLeak"
        };
        var englishRoutes = new[] { "au", "ca", "en", "gb", "za" };

        if (qa.SchemaVersion != 1 || !string.Equals(qa.Status, "PASS", StringComparison.Ordinal) ||
            !qa.Deployable || qa.ErrorCount != 0 || qa.RouteCount != 24 ||
            qa.DistinctSentenceCount != 20 || qa.TranslatedLanguageReviewCount != 19 ||
            qa.SourceEnglishReviewCount != 1 || qa.AuthorityTermReviewCount != 20 ||
            qa.SentenceReviewCount != 20 || qa.Reviews.Count != 20 ||
            qa.AdditionalEditCount != 3 || qa.AdditionalEditRowCount != 2 ||
            qa.BlockingFindings.Count != 0 || string.IsNullOrWhiteSpace(qa.Methodology) ||
            !string.Equals(qa.ReleaseDecision.Status, "PASS", StringComparison.Ordinal) ||
            !qa.ReleaseDecision.Deployable || string.IsNullOrWhiteSpace(qa.ReleaseDecision.Reason))
            throw new InvalidDataException("Product-prose canonical linguistic independent QA did not pass.");

        if (!string.Equals(qa.SourcePackage.File, "product-prose-koc-corrections.json",
                StringComparison.Ordinal) || !IsSha256(qa.SourcePackage.Sha256) ||
            !matchesEmbeddedSha256(qa.SourcePackage.File, qa.SourcePackage.Sha256) ||
            !string.Equals(qa.AuthorityPackage.File, "product-tag-localization.json",
                StringComparison.Ordinal) || qa.AuthorityPackage.EntityId != 583 ||
            !IsSha256(qa.AuthorityPackage.Sha256) ||
            !matchesEmbeddedSha256(qa.AuthorityPackage.File, qa.AuthorityPackage.Sha256) ||
            !string.Equals(qa.SupportingPackageBindings.ProductAttribute.File,
                "product-attribute-localization.json", StringComparison.Ordinal) ||
            !IsSha256(qa.SupportingPackageBindings.ProductAttribute.Sha256) ||
            !matchesEmbeddedSha256(qa.SupportingPackageBindings.ProductAttribute.File,
                qa.SupportingPackageBindings.ProductAttribute.Sha256) ||
            !string.Equals(qa.SupportingPackageBindings.UrduNockCorrection.File,
                "urdu-nock-linguistic-correction-audit.json", StringComparison.Ordinal) ||
            !IsSha256(qa.SupportingPackageBindings.UrduNockCorrection.Sha256) ||
            !matchesEmbeddedSha256(qa.SupportingPackageBindings.UrduNockCorrection.File,
                qa.SupportingPackageBindings.UrduNockCorrection.Sha256))
            throw new InvalidDataException("Product-prose canonical linguistic QA binding changed.");

        var authorityRows = productTags.Values.Where(value => value.EntityId == 583).ToList();
        var duplicateAuthorityRoute = authorityRows.GroupBy(value => value.LanguageCode,
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicateAuthorityRoute is not null || authorityRows.Count != 24)
            throw new InvalidDataException("ProductTag 583 linguistic authority routes are ambiguous.");
        var authorityByRoute = authorityRows.ToDictionary(value => value.LanguageCode,
            value => value.Value, StringComparer.Ordinal);
        if (!expectedRoutes.SetEquals(authorityByRoute.Keys) ||
            !expectedRoutes.SetEquals(productProse.CanonicalSentencesByLanguage.Keys))
            throw new InvalidDataException("Product-prose linguistic QA route coverage changed.");

        var routeCodes = qa.Reviews.SelectMany(review => review.RouteCodes).ToList();
        var duplicateReviewKey = qa.Reviews.GroupBy(review => review.ReviewKey,
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        var englishReview = qa.Reviews.SingleOrDefault(review =>
            string.Equals(review.ReviewKey, "en", StringComparison.Ordinal));
        if (duplicateReviewKey is not null || routeCodes.Count != 24 ||
            routeCodes.Distinct(StringComparer.Ordinal).Count() != 24 ||
            !expectedRoutes.SetEquals(routeCodes) || englishReview is null ||
            !englishReview.RouteCodes.SequenceEqual(englishRoutes, StringComparer.Ordinal) ||
            qa.Reviews.Where(review => !ReferenceEquals(review, englishReview)).Any(review =>
                review.RouteCodes.Count != 1 ||
                !string.Equals(review.RouteCodes[0], review.ReviewKey, StringComparison.Ordinal)) ||
            qa.Reviews.Select(review => review.CanonicalSentence)
                .Distinct(StringComparer.Ordinal).Count() != 20)
            throw new InvalidDataException("Product-prose linguistic QA review partition changed.");

        foreach (var review in qa.Reviews)
        {
            if (string.IsNullOrWhiteSpace(review.ReviewKey) ||
                string.IsNullOrWhiteSpace(review.LanguageName) ||
                string.IsNullOrWhiteSpace(review.AuthorityTerm) ||
                string.IsNullOrWhiteSpace(review.ComponentTerm) ||
                string.IsNullOrWhiteSpace(review.CanonicalSentence) ||
                string.IsNullOrWhiteSpace(review.Rationale) ||
                !string.Equals(review.Verdict, "PASS", StringComparison.Ordinal) ||
                review.Checks.Count != expectedCheckNames.Count ||
                !expectedCheckNames.SetEquals(review.Checks.Keys) || review.Checks.Values.Any(value => !value) ||
                review.ProhibitedAnimalOrNonArcheryTerms.Count == 0 ||
                review.ProhibitedAnimalOrNonArcheryTerms.Any(string.IsNullOrWhiteSpace) ||
                review.ProhibitedAnimalOrNonArcheryTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                    review.ProhibitedAnimalOrNonArcheryTerms.Count ||
                !review.CanonicalSentence.IsNormalized(NormalizationForm.FormC) ||
                !review.AuthorityTerm.IsNormalized(NormalizationForm.FormC) ||
                !IsSha256(review.CanonicalSentenceSha256) ||
                !Sha256(review.CanonicalSentence).Equals(review.CanonicalSentenceSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                CountOrdinal(review.CanonicalSentence, "Koç") != 1 ||
                CountOrdinal(review.AuthorityTerm, "Koç") != 1 ||
                CountOrdinal(review.CanonicalSentence, review.AuthorityTerm) != 1 ||
                review.AuthorityTerm.IndexOf(review.ComponentTerm,
                    StringComparison.OrdinalIgnoreCase) < 0 ||
                review.ProhibitedAnimalOrNonArcheryTerms.Any(term =>
                    ContainsReviewedProhibitedTerm(review.CanonicalSentence, term) ||
                    ContainsReviewedProhibitedTerm(review.AuthorityTerm, term)))
                throw new InvalidDataException(
                    $"Product-prose linguistic QA review changed: {review.ReviewKey}.");

            foreach (var routeCode in review.RouteCodes)
            {
                if (!authorityByRoute.TryGetValue(routeCode, out var authorityTerm) ||
                    !string.Equals(authorityTerm, review.AuthorityTerm, StringComparison.Ordinal) ||
                    !productProse.CanonicalSentencesByLanguage.TryGetValue(routeCode,
                        out var canonicalSentence) ||
                    !string.Equals(canonicalSentence, review.CanonicalSentence,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        $"Product-prose linguistic authority changed for route {routeCode}.");
            }
        }

        var residual = qa.DeployVisibleResidualAudit;
        if (residual.UrduProductProseNewValueAnatomicalNoseCount != 0 ||
            residual.UrduProductProseReplacementSpanAnatomicalNoseCount != 0 ||
            residual.UrduCanonicalSentenceAnatomicalNoseCount != 0 ||
            residual.UrduProductTagNockAnatomicalNoseCount != 0 ||
            residual.UrduProductAttributeNockAnatomicalNoseCount != 0 ||
            residual.LegacyOldValueAnatomicalNoseCount != 49 ||
            string.IsNullOrWhiteSpace(residual.LegacyOldValueDisposition))
            throw new InvalidDataException("Product-prose linguistic residual audit changed.");
    }

    private static bool ContainsReviewedProhibitedTerm(string value, string term) =>
        Regex.IsMatch(value,
            $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static void ValidateProductProseSupplementalPackage(
        ProductProseSupplementalPackage package)
    {
        var expectedFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Name"] = 8,
            ["ShortDescription"] = 6,
            ["FullDescription"] = 30
        };
        if (package is null || package.SchemaVersion != 2 || package.SlugPolicyVersion != 2 ||
            !string.Equals(package.Status, "PASS", StringComparison.Ordinal) ||
            !string.Equals(package.SourceDatabase,
                "HoodLocalizationPluginStage_Final_20260813", StringComparison.Ordinal) ||
            !string.Equals(package.SourceMode, "ApplicationIntentReadOnly", StringComparison.Ordinal) ||
            package.RowCount != 44 || package.ProductCount != 36 || package.LanguageCount != 1 ||
            package.Rows is null || package.Rows.Count != 44 ||
            package.FieldCounts is null ||
            !HaveExactCounts(package.FieldCounts, expectedFieldCounts) ||
            package.WitnessEntryCount != 9 || package.WitnessUniqueTupleCount != 7 ||
            package.WitnessPhraseOccurrenceCount != 21 ||
            package.PreservedWitnesses is null || package.PreservedWitnesses.Count != 9 ||
            package.SlugCount != 8 || package.Slugs is null || package.Slugs.Count != 8 ||
            !string.Equals(package.SlugTargetSetSha256,
                "70CE09B77B5AB4E42DBFBBE4E1C7494CC5C778665AA89DE748339E30422FEAC2",
                StringComparison.OrdinalIgnoreCase) ||
            !IsSha256(package.TargetTupleSetSha256) || package.EditCount != 65)
            throw new InvalidDataException(
                "Supplemental product-prose package coverage is incomplete.");
        ValidateProductProseSupplementalValidationSummary(package.Validation);

        var tupleLines = new List<string>();
        var tuples = new HashSet<string>(StringComparer.Ordinal);
        var identityLines = new List<string>();
        var productSkuById = new Dictionary<int, string>();
        var actualFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actualEditCount = 0;
        var reviewedFullReplacementCount = 0;
        foreach (var row in package.Rows)
        {
            var tuple = $"LocalizedProperty|{row.EntityId}|{row.LanguageCode}|{row.Field}";
            if (row.EntityId <= 0 || string.IsNullOrWhiteSpace(row.Sku) ||
                !string.Equals(row.LanguageCode, "ur", StringComparison.Ordinal) ||
                !expectedFieldCounts.ContainsKey(row.Field) || !tuples.Add(tuple) ||
                productSkuById.TryGetValue(row.EntityId, out var witnessedSku) &&
                !string.Equals(witnessedSku, row.Sku, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(row.OldValue) || string.IsNullOrWhiteSpace(row.NewValue) ||
                string.Equals(row.OldValue, row.NewValue, StringComparison.Ordinal) ||
                !Sha256(row.OldValue).Equals(row.OldSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !Sha256(row.NewValue).Equals(row.NewSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsSha256(row.OldHtmlTagSequenceSha256) ||
                !IsSha256(row.NewHtmlTagSequenceSha256) || row.Edits is null)
                throw new InvalidDataException(
                    $"Invalid supplemental product-prose correction tuple: {tuple}.");

            productSkuById[row.EntityId] = row.Sku;
            identityLines.Add($"LocalizedProperty|{row.EntityId}|{row.Sku}|{row.LanguageCode}|{row.Field}");
            actualFieldCounts[row.Field] = actualFieldCounts.GetValueOrDefault(row.Field) + 1;
            tupleLines.Add(tuple);
            var oldTagHash = ProductProseHtmlTagSequenceSha256(row.OldValue);
            var newTagHash = ProductProseHtmlTagSequenceSha256(row.NewValue);
            if (!oldTagHash.Equals(row.OldHtmlTagSequenceSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !newTagHash.Equals(row.NewHtmlTagSequenceSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Supplemental product-prose HTML evidence changed: {tuple}.");

            if (string.Equals(row.TransformationKind, "exactEdits", StringComparison.Ordinal))
            {
                if (!oldTagHash.Equals(newTagHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Supplemental exact edit changed HTML structure: {tuple}.");
                ValidateSupplementalExactEditTransformation(row);
            }
            else if (string.Equals(row.TransformationKind, "reviewedFullReplacement",
                         StringComparison.Ordinal))
            {
                reviewedFullReplacementCount++;
                if (row.EntityId != 212 ||
                    !string.Equals(row.Sku, "nock4", StringComparison.Ordinal) ||
                    !string.Equals(row.LanguageCode, "ur", StringComparison.Ordinal) ||
                    !string.Equals(row.Field, "FullDescription", StringComparison.Ordinal) ||
                    row.Edits.Count != 0 ||
                    oldTagHash.Equals(newTagHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Unapproved supplemental full-field replacement: {tuple}.");
            }
            else
                throw new InvalidDataException(
                    $"Unknown supplemental transformation kind: {tuple}.");

            actualEditCount += row.Edits.Count;
            if (ContainsProhibitedSupplementalUrduTerm(row.NewValue))
                throw new InvalidDataException(
                    $"Supplemental product-prose target retains a prohibited Urdu term: {tuple}.");
        }

        if (productSkuById.Count != 36 ||
            !HaveExactCounts(actualFieldCounts, expectedFieldCounts) ||
            actualEditCount != package.EditCount || reviewedFullReplacementCount != 1 ||
            !Sha256(string.Join("\n", identityLines.OrderBy(line => line, StringComparer.Ordinal)))
                .Equals("BE67735DF30BA67EA6638E68C18D876C4F1627509D0AC85E898862B13054460A",
                    StringComparison.OrdinalIgnoreCase) ||
            !Sha256(string.Join("\n", tupleLines.OrderBy(line => line, StringComparer.Ordinal)))
                .Equals(package.TargetTupleSetSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Supplemental product-prose tuple coverage changed.");

        ValidateSupplementalProductSlugs(package);

        var witnessTupleKeys = new HashSet<string>(StringComparer.Ordinal);
        var witnessPhraseKeys = new HashSet<string>(StringComparer.Ordinal);
        var witnessProductSkuById = new Dictionary<int, string>();
        var witnessPhraseOccurrenceCount = 0;
        foreach (var witness in package.PreservedWitnesses)
        {
            var tuple =
                $"LocalizedProperty|{witness.EntityId}|{witness.LanguageCode}|{witness.Field}";
            if (witness.EntityId <= 0 || string.IsNullOrWhiteSpace(witness.Sku) ||
                !string.Equals(witness.LanguageCode, "ur", StringComparison.Ordinal) ||
                !expectedFieldCounts.ContainsKey(witness.Field) ||
                string.IsNullOrWhiteSpace(witness.ExpectedValue) ||
                !Sha256(witness.ExpectedValue).Equals(witness.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(witness.Reason) || witness.Phrases is null ||
                witness.Phrases.Count == 0 || tuples.Contains(tuple) ||
                witnessProductSkuById.TryGetValue(witness.EntityId, out var witnessedSku) &&
                !string.Equals(witnessedSku, witness.Sku, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Invalid supplemental preserved witness: {tuple}.");

            witnessProductSkuById[witness.EntityId] = witness.Sku;
            witnessTupleKeys.Add(tuple);
            foreach (var phrase in witness.Phrases)
            {
                if (phrase is null || string.IsNullOrWhiteSpace(phrase.Phrase) ||
                    phrase.ExpectedOccurrenceCount <= 0 ||
                    CountOrdinal(witness.ExpectedValue, phrase.Phrase) !=
                    phrase.ExpectedOccurrenceCount ||
                    !witnessPhraseKeys.Add($"{tuple}|{phrase.Phrase}"))
                    throw new InvalidDataException(
                        $"Invalid supplemental preserved-witness phrase: {tuple}.");
                witnessPhraseOccurrenceCount += phrase.ExpectedOccurrenceCount;
            }
        }

        var inconsistentWitness = package.PreservedWitnesses.GroupBy(witness =>
                $"LocalizedProperty|{witness.EntityId}|{witness.LanguageCode}|{witness.Field}",
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Select(witness =>
                    $"{witness.Sku}\0{witness.ExpectedSha256}\0{witness.ExpectedValue}")
                .Distinct(StringComparer.Ordinal).Count() != 1);
        if (witnessTupleKeys.Count != 7 || witnessPhraseOccurrenceCount != 21 ||
            inconsistentWitness is not null)
            throw new InvalidDataException(
                "Supplemental preserved-witness coverage changed.");
    }

    internal static void ValidateSupplementalProductSlugs(ProductProseSupplementalPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var expectedIdentity = new Dictionary<int, (string Sku, string PreviousSlug)>
        {
            [212] = ("nock4", "کوک-نوک-لکڑی-اور-کاربن-یرو-نوک-ہارس-بیک-تیر-اندازی-کے-لیے-نصب-تیر-اندازی"),
            [214] = ("woodennock1", "ٹارگٹ-تیر-اندازی-کے-لیے-لکڑی-کا-سیلف-ناک-لکڑی-کا-تیر-کا-نشان"),
            [215] = ("nock7", "ٹارگٹ-تیر-اندازی-کے-لیے-بانس-شافٹ-لکڑی-کا-سیلف-ناک-لکڑی-کا-تیر-کا-نشان"),
            [222] = ("arrow105", "ریکرو-بو-لانگ-بو-پریمیم-سیریز-فیروزی-فیدر-ایرو-کے-لیے-لکڑی-کے-تیر-کا-تیر-سیاہ-فیروزی-پینٹ-شدہ-سیلف-ناک-کرسمس-گفٹ-کے-ساتھ"),
            [251] = ("arrow81", "لکڑی-کے-تیر-اندازی-کے-ساتھ-نیون-پیلے-ترکی-کے-پنکھوں-کے-ساتھ-سیلف-ناک-بیرلڈ-کرسٹڈ-تیر-تیر-اندازی-پریمیم-پرسنلائزڈ-ایرو-فار-ریکرو-بو-لانگ-بو-قرون-وسطی-کے-روایتی-عثمانی-شکاری-شوٹ"),
            [265] = ("arrow102", "لکڑی-کے-تیراندازی-کے-تیر-جس-میں-سرخ-سیاہ-ترکی-پنکھوں-کے-ساتھ-ہارن-ناک-کے-ساتھ-بیرل-والے-کرسٹڈ-تیر-تیر-اندازی-پریمیم-پرسنلائزڈ-ایرو-فار-ریکرو-بو-لانگ-بو-قرون-وسطی-کے-روایتی-عثمانی-شکاری-شوٹ"),
            [273] = ("arrow104", "لکڑی-کے-تیر-اندازی-پرسنلائزڈ-ایرو-بلیو-وائٹ-ترکی-فیدر-تذیب-آرٹ-سیلف-ناک-کے-ساتھ-پینٹ-کیا-گیا-ریکرو-بو-لانگ-بو-قرون-وسطی-کے-روایتی-عثمانی-شکار-کے-ساتھ-بلیو-ترکی-فیدر"),
            [301] = ("arrow45", "ریکرو-بو-لانگ-بو-پریمیم-سیریز-فیروزی-فیدر-ایرو-کے-لیے-لکڑی-کے-تیر-کا-تیر-سیاہ-فیروزی-پینٹ-شدہ-سیلف-ناک-کرسمس-گفٹ-کے-ساتھ-2")
        };
        if (package.SchemaVersion != 2 || package.SlugPolicyVersion != 2 ||
            package.SlugCount != 8 || package.Slugs is null || package.Slugs.Count != 8 ||
            package.Rows is null)
            throw new InvalidDataException(
                "Supplemental product-slug package coverage is incomplete.");

        var sourceNames = package.Rows.Where(row =>
                string.Equals(row.LanguageCode, "ur", StringComparison.Ordinal) &&
                string.Equals(row.Field, "Name", StringComparison.Ordinal))
            .ToDictionary(row => row.EntityId);
        var targetLines = new List<string>();
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in package.Slugs)
        {
            if (!expectedIdentity.TryGetValue(row.EntityId, out var expected) ||
                !string.Equals(row.Sku, expected.Sku, StringComparison.Ordinal) ||
                !string.Equals(row.LanguageCode, "ur", StringComparison.Ordinal) ||
                !string.Equals(row.SourceField, "Name", StringComparison.Ordinal) ||
                row.SlugPolicyVersion != 2 || !IsSha256(row.SourceValueSha256) ||
                !sourceNames.TryGetValue(row.EntityId, out var sourceName) ||
                !string.Equals(sourceName.Sku, row.Sku, StringComparison.Ordinal) ||
                !string.Equals(sourceName.NewSha256, row.SourceValueSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !Sha256(sourceName.NewValue).Equals(row.SourceValueSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsSemanticSlug(row.Slug) || !IsSemanticSlug(row.SlugBase) ||
                row.Slug.Length > 75 || row.SlugBase.Length > 75 ||
                ContainsProhibitedSupplementalUrduTerm(row.Slug) ||
                row.PreviousSlugs is null || row.PreviousSlugs.Count != 1 ||
                !string.Equals(row.PreviousSlugs[0], expected.PreviousSlug,
                    StringComparison.Ordinal) ||
                string.Equals(row.Slug, row.PreviousSlugs[0],
                    StringComparison.OrdinalIgnoreCase) ||
                !slugs.Add(row.Slug) || !previousSlugs.Add(row.PreviousSlugs[0]))
                throw new InvalidDataException(
                    $"Invalid supplemental product slug: Product/{row.EntityId}/ur.");

            var expectedSlug = row.EntityId == 301 ? $"{row.SlugBase}-2" : row.SlugBase;
            if (!string.Equals(row.Slug, expectedSlug, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Supplemental product slug collision policy changed: Product/{row.EntityId}/ur.");
            targetLines.Add(
                $"Product|{row.EntityId}|{row.LanguageCode.ToLowerInvariant()}|{row.Slug}|{row.SourceValueSha256}");
        }

        var targetSetSha256 = Sha256(string.Join("\n",
            targetLines.OrderBy(line => line, StringComparer.Ordinal)));
        if (slugs.Count != 8 || previousSlugs.Count != 8 || slugs.Overlaps(previousSlugs) ||
            sourceNames.Count != 8 || !expectedIdentity.Keys.OrderBy(id => id)
                .SequenceEqual(package.Slugs.Select(row => row.EntityId).OrderBy(id => id)) ||
            !targetSetSha256.Equals(package.SlugTargetSetSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !targetSetSha256.Equals(
                "70CE09B77B5AB4E42DBFBBE4E1C7494CC5C778665AA89DE748339E30422FEAC2",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Supplemental product-slug target coverage changed.");
    }

    internal static void ValidateSupplementalExactEditTransformation(
        ProductProseSupplementalCorrection row)
    {
        if (row.Edits is null || row.Edits.Count == 0)
            throw new InvalidDataException(
                "Supplemental exact-edit transformation has no reviewed edits.");
        var value = row.OldValue;
        foreach (var edit in row.Edits)
        {
            if (edit is null || string.IsNullOrWhiteSpace(edit.OldText) ||
                string.IsNullOrWhiteSpace(edit.NewText) || string.IsNullOrWhiteSpace(edit.Reason) ||
                string.Equals(edit.OldText, edit.NewText, StringComparison.Ordinal) ||
                CountOrdinal(value, edit.OldText) != 1)
                throw new InvalidDataException(
                    "Supplemental exact edit is blank, unchanged or ambiguous.");
            var index = value.IndexOf(edit.OldText, StringComparison.Ordinal);
            value = value.Remove(index, edit.OldText.Length).Insert(index, edit.NewText);
        }
        if (!string.Equals(value, row.NewValue, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Supplemental correction changes more than its reviewed exact edits.");
    }

    private static void ValidateProductProseSupplementalValidationSummary(
        IReadOnlyDictionary<string, JsonElement> validation)
    {
        var expectedNumbers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["errorCount"] = 0,
            ["exactTargetIdentityCount"] = 44,
            ["exactDistinctProductCount"] = 36,
            ["exactLanguageCount"] = 1,
            ["exactNameCount"] = 8,
            ["exactShortDescriptionCount"] = 6,
            ["exactFullDescriptionCount"] = 30,
            ["standaloneUrduNoseTargetCount"] = 15,
            ["incorrectUrduNockTermTargetCount"] = 31,
            ["issueSetOverlapCount"] = 2,
            ["exactEditsRowCount"] = 43,
            ["reviewedFullReplacementRowCount"] = 1,
            ["exactWitnessEntryCount"] = 9,
            ["exactWitnessUniqueTupleCount"] = 7,
            ["exactWitnessPhraseOccurrenceCount"] = 21,
            ["exactSlugCount"] = 8,
            ["exactSlugSourceNameCount"] = 8,
            ["exactSlugPreviousRedirectCount"] = 8,
            ["databaseWriteCount"] = 0
        };
        var expectedTrue = new[]
        {
            "noDuplicateTargetIdentities",
            "allOldHashesVerified",
            "allNewValuesDiffer",
            "allExactEditsReplay",
            "allExactEditHtmlTagSequencesPreserved",
            "reviewedFullReplacementExactlyAllowlisted",
            "noBroadReplacement",
            "allOldSlugsOwnedByTarget",
            "allDesiredSlugsUnownedOrSameOwner",
            "noProhibitedTermsInDesiredSlugs"
        };
        if (validation is null ||
            validation.Count != expectedNumbers.Count + expectedTrue.Length + 1 ||
            !validation.TryGetValue("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            !string.Equals(status.GetString(), "PASS", StringComparison.Ordinal) ||
            expectedNumbers.Any(pair =>
                !validation.TryGetValue(pair.Key, out var value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var actual) ||
                actual != pair.Value) ||
            expectedTrue.Any(key => !validation.TryGetValue(key, out var value) ||
                value.ValueKind is not JsonValueKind.True))
            throw new InvalidDataException(
                "Supplemental product-prose self-validation summary changed.");
    }

    private static string ProductProseHtmlTagSequenceSha256(string value) =>
        Sha256(string.Join('\u001f', Regex.Matches(value ?? string.Empty, "<[^>]+>")
            .Select(match => match.Value)));

    private static bool ContainsProhibitedSupplementalUrduTerm(string value) =>
        ContainsReviewedProhibitedTerm(value, "ناک") ||
        ContainsReviewedProhibitedTerm(value, "کوک") ||
        ContainsReviewedProhibitedTerm(value, "نوکنگ") ||
        Regex.IsMatch(value ?? string.Empty, @"ن\u0640+(?:اک|وک)",
            RegexOptions.CultureInvariant);

    internal static void ValidateProductProseKocPackage(ProductProseKocPackage package,
        ProductTagPackage productTags)
    {
        var expectedCodes = productTags.Values.Where(value => value.EntityId == 583)
            .ToDictionary(value => value.LanguageCode, value => value.Value,
                StringComparer.OrdinalIgnoreCase);
        var expectedValidation = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["errorCount"] = 0,
            ["duplicateTupleCount"] = 0,
            ["ambiguousDetectionCount"] = 0,
            ["missingExpectedChoiceSentenceCount"] = 0,
            ["residualLiteralRamNockCount"] = 0,
            ["htmlStructureMismatchCount"] = 0,
            ["authorityMismatchCount"] = 0,
            ["unreviewedFalsePositiveCount"] = 0,
            ["unselectedContextualWrongTermCount"] = 0,
            ["targetedResidualWrongTermCount"] = 0,
            ["targetedUrduAnatomicalNoseResidualCount"] = 0,
            ["reviewedAdditionalEditCount"] = 3
        };
        var expectedInvariantFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ShortDescription"] = 51,
            ["FullDescription"] = 62
        };
        var expectedLocalizedFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ShortDescription"] = 1224,
            ["FullDescription"] = 1512
        };
        var expectedDetectionCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["literalRamNock"] = 601,
            ["semanticAnimalMistranslation"] = 432,
            ["naturalSentenceNormalization"] = 1816
        };
        var expectedFalsePositiveTuples = new HashSet<
            (int EntityId, string Sku, string LanguageCode, string Field, string Phrase,
                int PhraseOccurrenceCount)>
        {
            (60, "HoodSword3", "es", "FullDescription", "Telchar carneron", 1),
            (60, "HoodSword3", "es", "ShortDescription", "Telchar carneron", 1),
            (328, "leatherpant1", "pt", "FullDescription", "pele de carneiro", 2),
            (328, "leatherpant1", "pt", "ShortDescription", "pele de carneiro", 1)
        };
        if (package.SchemaVersion != 2 || !package.Status.Equals("PASS", StringComparison.Ordinal) ||
            !package.SourceMode.Equals("ReadOnly", StringComparison.Ordinal) ||
            !package.SourceDatabase.Equals("HoodLocalizationPluginStage_Final_20260813",
                StringComparison.Ordinal) || package.RowCount != 2849 ||
            package.InvariantCorrectionCount != 113 || package.LocalizedCorrectionCount != 2736 ||
            package.AdditionalEditCount != 3 || package.AdditionalEditRowCount != 2 ||
            package.ProductCount != 63 || package.LanguageCount != 24 ||
            package.TargetProducts.Count != 63 || package.Rows.Count != 2849 ||
            !IsSha256(package.TargetTupleSetSha256) || package.ContextualWrongTermMatchCount != 1459 ||
            package.TurkishKocNockSourceCount != 64 || package.TurkishKocCostnockSourceCount != 6 ||
            package.PreservedFalsePositiveCount != 4 || package.PreservedFalsePositives.Count != 4 ||
            !HaveExactCounts(package.InvariantFieldCounts, expectedInvariantFieldCounts) ||
            !HaveExactCounts(package.LocalizedFieldCounts, expectedLocalizedFieldCounts) ||
            !HaveExactCounts(package.DetectionCounts, expectedDetectionCounts) ||
            package.PerLanguageTargetCounts.Count != 24 ||
            package.PerLanguageFieldCounts.Count != 24 ||
            expectedCodes.Keys.Any(code =>
                package.PerLanguageTargetCounts.GetValueOrDefault(code) != 114 ||
                !package.PerLanguageFieldCounts.TryGetValue(code, out var fieldCounts) ||
                fieldCounts.Count != 2 || fieldCounts.GetValueOrDefault("ShortDescription") != 51 ||
                fieldCounts.GetValueOrDefault("FullDescription") != 63) ||
            package.Validation.Count != expectedValidation.Count ||
            expectedValidation.Any(pair => package.Validation.GetValueOrDefault(pair.Key, -1) != pair.Value))
            throw new InvalidDataException("Product-prose Koç package coverage is incomplete.");

        var acceptedPreviousRows = package.Rows.Where(row =>
            row.AcceptedPreviousValues is { Count: > 0 }).ToList();
        if (acceptedPreviousRows.Count != 1)
            throw new InvalidDataException(
                "Product-prose accepted previous-state coverage changed.");

        if (expectedCodes.Count != 24 || package.CanonicalSentencesByLanguage.Count != 24 ||
            package.ReplacementAuthority.EntityId != 583 ||
            !package.ReplacementAuthority.File.Equals("product-tag-localization.json",
                StringComparison.Ordinal) ||
            !package.ReplacementAuthority.Sha256.Equals(
                Convert.ToHexString(SHA256.HashData(ReadEmbeddedBytes("product-tag-localization.json"))),
                StringComparison.OrdinalIgnoreCase) ||
            package.ReplacementAuthority.ValuesByLanguage.Count != 24 ||
            expectedCodes.Any(pair =>
                !package.ReplacementAuthority.ValuesByLanguage.TryGetValue(pair.Key, out var authority) ||
                !string.Equals(authority, pair.Value, StringComparison.Ordinal) ||
                !package.CanonicalSentencesByLanguage.TryGetValue(pair.Key, out var sentence) ||
                !sentence.Contains(pair.Value, StringComparison.Ordinal)))
            throw new InvalidDataException("Product-prose Koç terminology authority changed.");

        var targetProducts = package.TargetProducts.GroupBy(item => item.EntityId).ToList();
        if (targetProducts.Count != 63 || targetProducts.Any(group => group.Count() != 1 ||
                string.IsNullOrWhiteSpace(group.Single().Sku)))
            throw new InvalidDataException("Product-prose product identity witnesses are invalid.");
        var skuByProductId = package.TargetProducts.ToDictionary(item => item.EntityId, item => item.Sku);
        var tupleLines = new List<string>();
        var localizedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var localizedShortCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var localizedFullCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var actualInvariantFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actualLocalizedFieldCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actualDetectionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actualProductIds = new HashSet<int>();
        var actualInvariantCount = 0;
        var actualLocalizedCount = 0;
        var actualAdditionalEditCount = 0;
        var actualAdditionalEditRowCount = 0;
        var tuples = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in package.Rows)
        {
            var invariant = string.Equals(row.TargetKind, "InvariantProduct", StringComparison.Ordinal);
            var localized = string.Equals(row.TargetKind, "LocalizedProperty", StringComparison.Ordinal);
            var code = invariant ? "invariant" : row.LanguageCode;
            var tuple = $"{row.TargetKind}|{row.EntityId}|{code}|{row.Field}";
            ValidateProductProseAcceptedPreviousValues(row);
            if ((!invariant && !localized) || row.EntityId <= 0 || !tuples.Add(tuple) ||
                !skuByProductId.TryGetValue(row.EntityId, out var expectedSku) ||
                !string.Equals(row.Sku, expectedSku, StringComparison.Ordinal) ||
                invariant && row.LanguageCode is not null || localized &&
                (string.IsNullOrWhiteSpace(row.LanguageCode) || !expectedCodes.ContainsKey(row.LanguageCode)) ||
                row.Field is not ("ShortDescription" or "FullDescription") ||
                string.IsNullOrWhiteSpace(row.DetectionKind) ||
                !expectedDetectionCounts.ContainsKey(row.DetectionKind) ||
                row.OldChoiceSentenceCount != 1 || row.NewCanonicalSentenceCount != 1 ||
                string.IsNullOrWhiteSpace(row.OldValue) || string.IsNullOrWhiteSpace(row.NewValue) ||
                string.Equals(row.OldValue, row.NewValue, StringComparison.Ordinal) ||
                !Sha256(row.OldValue).Equals(row.OldSha256, StringComparison.OrdinalIgnoreCase) ||
                !Sha256(row.NewValue).Equals(row.NewSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Invalid product-prose correction tuple: {tuple}.");

            actualProductIds.Add(row.EntityId);
            actualDetectionCounts[row.DetectionKind] =
                actualDetectionCounts.GetValueOrDefault(row.DetectionKind) + 1;
            var actualFieldCounts = invariant ? actualInvariantFieldCounts : actualLocalizedFieldCounts;
            actualFieldCounts[row.Field] = actualFieldCounts.GetValueOrDefault(row.Field) + 1;
            if (invariant)
                actualInvariantCount++;
            else
                actualLocalizedCount++;

            var sentenceCode = invariant ? "en" : row.LanguageCode;
            if (!package.CanonicalSentencesByLanguage.TryGetValue(sentenceCode, out var sentence))
                throw new InvalidDataException($"Invalid product-prose replacement: {tuple}.");
            ValidateProductProseCorrectionTransformation(row, sentence);
            actualAdditionalEditCount += row.AdditionalEdits.Count;
            if (row.AdditionalEdits.Count > 0)
                actualAdditionalEditRowCount++;

            var oldTags = Regex.Matches(row.OldValue, "<[^>]+>").Select(match => match.Value).ToArray();
            var newTags = Regex.Matches(row.NewValue, "<[^>]+>").Select(match => match.Value).ToArray();
            var tagHash = Sha256(string.Join('\u001f', oldTags));
            if (!oldTags.SequenceEqual(newTags, StringComparer.Ordinal) ||
                !tagHash.Equals(row.HtmlTagSequenceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Product-prose correction changed HTML: {tuple}.");

            tupleLines.Add(tuple);
            if (localized)
            {
                localizedCounts[row.LanguageCode] = localizedCounts.GetValueOrDefault(row.LanguageCode) + 1;
                var fieldCounts = row.Field == "ShortDescription" ? localizedShortCounts : localizedFullCounts;
                fieldCounts[row.LanguageCode] = fieldCounts.GetValueOrDefault(row.LanguageCode) + 1;
            }
        }

        if (actualInvariantCount != package.InvariantCorrectionCount ||
            actualLocalizedCount != package.LocalizedCorrectionCount ||
            !HaveExactCounts(actualInvariantFieldCounts, package.InvariantFieldCounts) ||
            !HaveExactCounts(actualLocalizedFieldCounts, package.LocalizedFieldCounts) ||
            !HaveExactCounts(actualDetectionCounts, package.DetectionCounts) ||
            actualAdditionalEditCount != package.AdditionalEditCount ||
            actualAdditionalEditRowCount != package.AdditionalEditRowCount ||
            !actualProductIds.SetEquals(skuByProductId.Keys) ||
            localizedCounts.Count != 24 || expectedCodes.Keys.Any(code =>
                localizedCounts.GetValueOrDefault(code) != 114 ||
                localizedShortCounts.GetValueOrDefault(code) != 51 ||
                localizedFullCounts.GetValueOrDefault(code) != 63 ||
                localizedCounts.GetValueOrDefault(code) !=
                package.PerLanguageTargetCounts.GetValueOrDefault(code) ||
                !package.PerLanguageFieldCounts.TryGetValue(code, out var declaredFieldCounts) ||
                localizedShortCounts.GetValueOrDefault(code) !=
                declaredFieldCounts.GetValueOrDefault("ShortDescription") ||
                localizedFullCounts.GetValueOrDefault(code) !=
                declaredFieldCounts.GetValueOrDefault("FullDescription")) ||
            !Sha256(string.Join('\n', tupleLines.OrderBy(line => line, StringComparer.Ordinal)))
                .Equals(package.TargetTupleSetSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Product-prose tuple coverage changed.");

        var falsePositiveOccurrences = 0;
        var falsePositiveTuples = new HashSet<
            (int EntityId, string Sku, string LanguageCode, string Field, string Phrase,
                int PhraseOccurrenceCount)>();
        var falsePositiveSkuByProductId = new Dictionary<int, string>();
        foreach (var witness in package.PreservedFalsePositives)
        {
            var witnessTuple = (witness.EntityId, witness.Sku, witness.LanguageCode, witness.Field,
                witness.Phrase, witness.PhraseOccurrenceCount);
            var correctionTuple =
                $"LocalizedProperty|{witness.EntityId}|{witness.LanguageCode}|{witness.Field}";
            if (witness.EntityId <= 0 || string.IsNullOrWhiteSpace(witness.Sku) ||
                string.IsNullOrWhiteSpace(witness.LanguageCode) ||
                witness.Field is not ("ShortDescription" or "FullDescription") ||
                string.IsNullOrWhiteSpace(witness.Phrase) || witness.PhraseOccurrenceCount <= 0 ||
                string.IsNullOrWhiteSpace(witness.ExpectedValue) ||
                string.IsNullOrWhiteSpace(witness.Reason) ||
                !falsePositiveTuples.Add(witnessTuple) ||
                !expectedFalsePositiveTuples.Contains(witnessTuple) ||
                tuples.Contains(correctionTuple) ||
                falsePositiveSkuByProductId.TryGetValue(witness.EntityId, out var witnessedSku) &&
                !string.Equals(witnessedSku, witness.Sku, StringComparison.Ordinal) ||
                !Sha256(witness.ExpectedValue).Equals(witness.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                CountOrdinal(witness.ExpectedValue, witness.Phrase) != witness.PhraseOccurrenceCount)
                throw new InvalidDataException("Product-prose preserved false-positive witness is invalid.");
            falsePositiveSkuByProductId[witness.EntityId] = witness.Sku;
            falsePositiveOccurrences += witness.PhraseOccurrenceCount;
        }
        if (falsePositiveOccurrences != 5 ||
            !falsePositiveTuples.SetEquals(expectedFalsePositiveTuples))
            throw new InvalidDataException("Product-prose preserved false-positive coverage changed.");
    }

    internal static void ValidateProductProseCorrectionTransformation(ProductProseKocCorrection row,
        string canonicalSentence)
    {
        if (!string.Equals(row.CanonicalSentence, canonicalSentence, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(row.OldSentence) || string.IsNullOrWhiteSpace(row.ReplacementSpan) ||
            row.AdditionalEdits is null ||
            CountOrdinal(row.OldValue, row.OldSentence) != 1 ||
            CountOrdinal(row.ReplacementSpan, canonicalSentence) != 1 ||
            CountOrdinal(row.NewValue, canonicalSentence) != 1 ||
            Regex.IsMatch(row.NewValue, @"(?<![\p{L}\p{N}_])ram\s+nock(?![\p{L}\p{N}_])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException("Product-prose correction transformation is invalid.");

        var oldSentenceIndex = row.OldValue.IndexOf(row.OldSentence, StringComparison.Ordinal);
        var sentenceEditedValue = row.OldValue.Remove(oldSentenceIndex, row.OldSentence.Length)
            .Insert(oldSentenceIndex, row.ReplacementSpan);
        var replacementStart = oldSentenceIndex;
        var replacementEnd = replacementStart + row.ReplacementSpan.Length;
        var reviewedRanges = new List<(int Start, int End)>();
        var reviewedOldTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edit in row.AdditionalEdits)
        {
            if (edit is null || string.IsNullOrWhiteSpace(edit.OldText) ||
                string.IsNullOrWhiteSpace(edit.NewText) || string.IsNullOrWhiteSpace(edit.Reason) ||
                string.Equals(edit.OldText, edit.NewText, StringComparison.Ordinal) ||
                !reviewedOldTexts.Add(edit.OldText) ||
                CountOrdinal(sentenceEditedValue, edit.OldText) != 1 ||
                CountOrdinal(sentenceEditedValue, edit.NewText) != 0)
                throw new InvalidDataException(
                    "Product-prose additional edit is blank, duplicate or ambiguous.");

            var editStart = sentenceEditedValue.IndexOf(edit.OldText, StringComparison.Ordinal);
            var editEnd = editStart + edit.OldText.Length;
            if (RangesOverlap(editStart, editEnd, replacementStart, replacementEnd) ||
                reviewedRanges.Any(range => RangesOverlap(editStart, editEnd, range.Start, range.End)))
                throw new InvalidDataException("Product-prose additional edits overlap.");
            reviewedRanges.Add((editStart, editEnd));
        }

        var expectedNewValue = sentenceEditedValue;
        foreach (var edit in row.AdditionalEdits)
        {
            if (CountOrdinal(expectedNewValue, edit.OldText) != 1)
                throw new InvalidDataException("Product-prose additional edit order is ambiguous.");
            var editStart = expectedNewValue.IndexOf(edit.OldText, StringComparison.Ordinal);
            expectedNewValue = expectedNewValue.Remove(editStart, edit.OldText.Length)
                .Insert(editStart, edit.NewText);
            if (CountOrdinal(expectedNewValue, edit.OldText) != 0 ||
                CountOrdinal(expectedNewValue, edit.NewText) != 1)
                throw new InvalidDataException("Product-prose additional edit result is ambiguous.");
        }

        if (!string.Equals(expectedNewValue, row.NewValue, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Product-prose correction changes more than its reviewed sentence and additional edits.");
    }

    private static bool RangesOverlap(int firstStart, int firstEnd, int secondStart, int secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    private static int CountOrdinal(string value, string fragment)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(fragment))
            return 0;
        var count = 0;
        for (var index = 0; (index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0;
             index += fragment.Length)
            count++;
        return count;
    }

    private static bool HaveExactCounts(IReadOnlyDictionary<string, int> actual,
        IReadOnlyDictionary<string, int> expected) =>
        actual.Count == expected.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out var count) && count == pair.Value);

    internal static void ValidateDisavowedForeignPreviousSlugs(LocalizationManifest manifest,
        ProductTagPackage productTags)
    {
        if (manifest.DisavowedForeignPreviousSlugs is null ||
            manifest.DisavowedForeignPreviousSlugCount != 20 ||
            manifest.DisavowedForeignPreviousSlugs.Count != manifest.DisavowedForeignPreviousSlugCount)
            throw new InvalidDataException("Disavowed foreign previous-slug evidence is incomplete.");

        var duplicateEvidence = manifest.DisavowedForeignPreviousSlugs
            .GroupBy(item => ((item.EntityType ?? string.Empty).ToLowerInvariant(), item.EntityId, item.LanguageId,
                Slug: (item.Slug ?? string.Empty).ToLowerInvariant()))
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicateEvidence is not null)
            throw new InvalidDataException("Disavowed foreign previous-slug evidence contains duplicates.");

        var sameOwnerRecordIds = new List<int>();
        var foreignOwnerRecordIds = new List<int>();
        foreach (var evidence in manifest.DisavowedForeignPreviousSlugs)
        {
            if (string.IsNullOrWhiteSpace(evidence.EntityType) || evidence.EntityId <= 0 ||
                evidence.LanguageId <= 0 || string.IsNullOrWhiteSpace(evidence.LanguageCode) ||
                string.IsNullOrWhiteSpace(evidence.Slug) || evidence.Slug.Length > 200 ||
                !string.Equals(evidence.Reason, "foreign-owner-preserved", StringComparison.Ordinal) ||
                evidence.SameOwnerUrlRecordIds is null || evidence.SameOwnerUrlRecordIds.Count == 0 ||
                evidence.SameOwnerUrlRecordIds.Any(id => id <= 0) ||
                evidence.SameOwnerUrlRecordIds.Distinct().Count() != evidence.SameOwnerUrlRecordIds.Count ||
                evidence.ForeignOwners is null || evidence.ForeignOwners.Count == 0 ||
                evidence.ForeignOwners.Any(owner => owner.UrlRecordId <= 0 || owner.EntityId <= 0 ||
                    owner.LanguageId <= 0 || owner.LanguageId != evidence.LanguageId ||
                    string.IsNullOrWhiteSpace(owner.EntityType) ||
                    string.Equals(owner.EntityType, evidence.EntityType, StringComparison.OrdinalIgnoreCase) &&
                    owner.EntityId == evidence.EntityId || !owner.IsActive.HasValue) ||
                evidence.ForeignOwners.Select(owner => owner.UrlRecordId).Distinct().Count() !=
                evidence.ForeignOwners.Count)
                throw new InvalidDataException("Disavowed foreign previous-slug evidence is invalid.");

            sameOwnerRecordIds.AddRange(evidence.SameOwnerUrlRecordIds);
            foreignOwnerRecordIds.AddRange(evidence.ForeignOwners.Select(owner => owner.UrlRecordId));
        }

        if (sameOwnerRecordIds.Distinct().Count() != sameOwnerRecordIds.Count ||
            foreignOwnerRecordIds.Distinct().Count() != foreignOwnerRecordIds.Count ||
            sameOwnerRecordIds.Intersect(foreignOwnerRecordIds).Any())
            throw new InvalidDataException("Disavowed foreign previous-slug URL-record witnesses are ambiguous.");

        var claimedSlugs = manifest.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Slug))
            .SelectMany(entry => new[] { entry.Slug }.Concat(entry.PreviousSlugs))
            .Concat(productTags.Slugs.SelectMany(entry =>
                new[] { entry.Slug }.Concat(entry.PreviousSlugs)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (manifest.DisavowedForeignPreviousSlugs.Any(evidence => claimedSlugs.Contains(evidence.Slug)))
            throw new InvalidDataException("A disavowed foreign previous slug is still claimed by the package.");
    }

    internal static void ValidateRetiredUrlRecordPackageContract(IList<int> retiredUrlRecordIds,
        IList<RetiredUrlRecord> retiredUrlRecords)
    {
        if (retiredUrlRecordIds is null || retiredUrlRecords is null ||
            retiredUrlRecordIds.Count != 1 || retiredUrlRecords.Count != 1 ||
            retiredUrlRecordIds[0] != 708)
            throw new InvalidDataException("Reviewed retired URL package contract did not pass.");

        var record = retiredUrlRecords[0];
        if (record.Id != 708 || record.EntityId != 443 || record.LanguageId != 0 ||
            !string.Equals(record.EntityName, "ProductTag", StringComparison.Ordinal) ||
            !string.Equals(record.Slug, "kleidung-mittelalter", StringComparison.Ordinal))
            throw new InvalidDataException("Reviewed retired URL package contract did not pass.");
    }

    internal static void ValidateCoverageContract(int manifestLocalizedEntryCount,
        int manifestSlugValueLinkCheckCount, int productTagSlugValueLinkCheckCount,
        int slugValueLinkCheckCount, int actualManifestEntryCount,
        int actualManifestSlugValueLinkCheckCount, int actualProductTagSlugValueLinkCheckCount,
        int productAttributeSchemaVersion)
    {
        if (manifestLocalizedEntryCount != 1168 || actualManifestEntryCount != manifestLocalizedEntryCount ||
            manifestSlugValueLinkCheckCount != 1120 ||
            actualManifestSlugValueLinkCheckCount != manifestSlugValueLinkCheckCount ||
            productTagSlugValueLinkCheckCount != 819 * 24 ||
            actualProductTagSlugValueLinkCheckCount != productTagSlugValueLinkCheckCount ||
            slugValueLinkCheckCount != manifestSlugValueLinkCheckCount + productTagSlugValueLinkCheckCount ||
            productAttributeSchemaVersion != 2)
            throw new InvalidDataException("Localization package coverage contract did not pass.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool IsSha256(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 64 &&
        value.All(character => char.IsAsciiHexDigit(character));

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsSemanticSlug(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetter);

    private static bool IsReservedSlugVariant(string slug, string slugBase)
    {
        if (slug.Equals(slugBase, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!slug.StartsWith(slugBase + "-", StringComparison.OrdinalIgnoreCase))
            return false;
        var suffix = slug[(slugBase.Length + 1)..];
        return int.TryParse(suffix, out var suffixNumber) && suffixNumber >= 2;
    }

    internal sealed class LocalizationManifest
    {
        public int SchemaVersion { get; set; }
        public int SlugPolicyVersion { get; set; }
        public List<ManifestEntry> Entries { get; set; } = new();
        public List<int> RetiredUrlRecordIds { get; set; } = new();
        public List<RetiredUrlRecord> RetiredUrlRecords { get; set; } = new();
        public int DisavowedForeignPreviousSlugCount { get; set; }
        public List<DisavowedForeignPreviousSlug> DisavowedForeignPreviousSlugs { get; set; } = new();
        public ValidationResult Validation { get; set; } = new();
    }
    internal sealed class DisavowedForeignPreviousSlug
    {
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string Slug { get; set; }
        public string Reason { get; set; }
        public List<int> SameOwnerUrlRecordIds { get; set; } = new();
        public List<ForeignSlugOwner> ForeignOwners { get; set; } = new();
    }
    internal sealed class ForeignSlugOwner
    {
        public int UrlRecordId { get; set; }
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public bool? IsActive { get; set; }
    }
    internal sealed class RetiredUrlRecord
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string EntityName { get; set; }
        public string Slug { get; set; }
        public int LanguageId { get; set; }
    }
    internal sealed class ManifestEntry
    {
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string SourceHash { get; set; }
        public string Sku { get; set; }
        public string SystemName { get; set; }
        public int SourceLanguageId { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
        public string Slug { get; set; }
        public int SlugPolicyVersion { get; set; }
        public string SlugPolicy { get; set; }
        public string SlugSource { get; set; }
        public string SlugSourceSha256 { get; set; }
        public string SlugBase { get; set; }
        public List<string> PreviousSlugs { get; set; } = new();
    }
    private sealed class UiResourcePackage
    {
        public int LanguageCount { get; set; }
        public int ResourceCountPerLanguage { get; set; }
        public List<UiResourceEntry> Entries { get; set; } = new();
    }
    private sealed class UiResourceEntry
    {
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string ResourceName { get; set; }
        public string ResourceValue { get; set; }
    }
    internal sealed class ProductTagPackage
    {
        public int SchemaVersion { get; set; }
        public int SlugPolicyVersion { get; set; }
        public int ValueSlugRegenerationCheckCount { get; set; }
        public int NumericOnlySlugCount { get; set; }
        public int RetainedPreviousSlugCount { get; set; }
        public int TagCount { get; set; }
        public int LanguageCount { get; set; }
        public List<ProductTagValue> Values { get; set; } = new();
        public List<ProductTagSlug> Slugs { get; set; } = new();
        public ValidationResult Audit { get; set; } = new();
    }
    internal sealed class ProductTagValue
    {
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string Source { get; set; }
        public string Value { get; set; }
    }
    internal sealed class ProductTagSlug
    {
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string Slug { get; set; }
        public int SlugPolicyVersion { get; set; }
        public string ValueSha256 { get; set; }
        public string SlugBase { get; set; }
        public List<string> PreviousSlugs { get; set; } = new();
    }
    private sealed class ProductAttributePackage
    {
        public int SchemaVersion { get; set; }
        public int AttributeCount { get; set; }
        public int AttributeValueCount { get; set; }
        public int MappingCount { get; set; }
        public int LanguageCount { get; set; }
        public int LocalizedValueCount { get; set; }
        public List<ProductAttributeLocalizedValue> Rows { get; set; } = new();
        public ValidationResult Audit { get; set; } = new();
    }
    private sealed class ProductAttributeLocalizedValue
    {
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageCode { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string Source { get; set; }
        public string Value { get; set; }
    }
    internal sealed class ProductProseCanonicalLinguisticQa
    {
        public int SchemaVersion { get; set; }
        public string Status { get; set; }
        public LinguisticPackageBinding SourcePackage { get; set; } = new();
        public LinguisticPackageBinding AuthorityPackage { get; set; } = new();
        public LinguisticSupportingPackageBindings SupportingPackageBindings { get; set; } = new();
        public int RouteCount { get; set; }
        public int DistinctSentenceCount { get; set; }
        public int TranslatedLanguageReviewCount { get; set; }
        public int SourceEnglishReviewCount { get; set; }
        public int AuthorityTermReviewCount { get; set; }
        public int SentenceReviewCount { get; set; }
        public int AdditionalEditCount { get; set; }
        public int AdditionalEditRowCount { get; set; }
        public int ErrorCount { get; set; }
        public bool Deployable { get; set; }
        public string Methodology { get; set; }
        public LinguisticDeployVisibleResidualAudit DeployVisibleResidualAudit { get; set; } = new();
        public List<LinguisticReview> Reviews { get; set; } = new();
        public List<JsonElement> BlockingFindings { get; set; } = new();
        public LinguisticReleaseDecision ReleaseDecision { get; set; } = new();
    }
    internal sealed class LinguisticPackageBinding
    {
        public string File { get; set; }
        public string Sha256 { get; set; }
        public int EntityId { get; set; }
    }
    internal sealed class LinguisticSupportingPackageBindings
    {
        public LinguisticPackageBinding ProductAttribute { get; set; } = new();
        public LinguisticPackageBinding UrduNockCorrection { get; set; } = new();
    }
    internal sealed class LinguisticDeployVisibleResidualAudit
    {
        public int UrduProductProseNewValueAnatomicalNoseCount { get; set; }
        public int UrduProductProseReplacementSpanAnatomicalNoseCount { get; set; }
        public int UrduCanonicalSentenceAnatomicalNoseCount { get; set; }
        public int UrduProductTagNockAnatomicalNoseCount { get; set; }
        public int UrduProductAttributeNockAnatomicalNoseCount { get; set; }
        public int LegacyOldValueAnatomicalNoseCount { get; set; }
        public string LegacyOldValueDisposition { get; set; }
    }
    internal sealed class LinguisticReview
    {
        public string ReviewKey { get; set; }
        public List<string> RouteCodes { get; set; } = new();
        public string LanguageName { get; set; }
        public string AuthorityTerm { get; set; }
        public string ComponentTerm { get; set; }
        public string CanonicalSentence { get; set; }
        public string CanonicalSentenceSha256 { get; set; }
        public List<string> ProhibitedAnimalOrNonArcheryTerms { get; set; } = new();
        public Dictionary<string, bool> Checks { get; set; } = new(StringComparer.Ordinal);
        public string Verdict { get; set; }
        public string Rationale { get; set; }
    }
    internal sealed class LinguisticReleaseDecision
    {
        public string Status { get; set; }
        public bool Deployable { get; set; }
        public string Reason { get; set; }
    }
    internal sealed class ProductProseKocPackage
    {
        public int SchemaVersion { get; set; }
        public string Status { get; set; }
        public string SourceDatabase { get; set; }
        public string SourceMode { get; set; }
        public int RowCount { get; set; }
        public int InvariantCorrectionCount { get; set; }
        public int LocalizedCorrectionCount { get; set; }
        public int AdditionalEditCount { get; set; }
        public int AdditionalEditRowCount { get; set; }
        public int ProductCount { get; set; }
        public int LanguageCount { get; set; }
        public string TargetTupleSetSha256 { get; set; }
        public List<ProductProseTargetProduct> TargetProducts { get; set; } = new();
        public Dictionary<string, int> InvariantFieldCounts { get; set; } = new();
        public Dictionary<string, int> LocalizedFieldCounts { get; set; } = new();
        public Dictionary<string, int> PerLanguageTargetCounts { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> PerLanguageFieldCounts { get; set; } = new();
        public Dictionary<string, int> DetectionCounts { get; set; } = new();
        public int ContextualWrongTermMatchCount { get; set; }
        public int TurkishKocNockSourceCount { get; set; }
        public int TurkishKocCostnockSourceCount { get; set; }
        public ProductProseReplacementAuthority ReplacementAuthority { get; set; } = new();
        public Dictionary<string, string> CanonicalSentencesByLanguage { get; set; } = new();
        public int PreservedFalsePositiveCount { get; set; }
        public List<ProductProseFalsePositive> PreservedFalsePositives { get; set; } = new();
        public List<ProductProseKocCorrection> Rows { get; set; } = new();
        public Dictionary<string, double> Validation { get; set; } = new();
    }
    internal sealed class ProductProseTargetProduct
    {
        public int EntityId { get; set; }
        public string Sku { get; set; }
    }
    internal sealed class ProductProseReplacementAuthority
    {
        public string File { get; set; }
        public string Sha256 { get; set; }
        public int EntityId { get; set; }
        public Dictionary<string, string> ValuesByLanguage { get; set; } = new();
    }
    internal sealed class ProductProseFalsePositive
    {
        public int EntityId { get; set; }
        public string Sku { get; set; }
        public string LanguageCode { get; set; }
        public string Field { get; set; }
        public string Phrase { get; set; }
        public int PhraseOccurrenceCount { get; set; }
        public string ExpectedSha256 { get; set; }
        public string ExpectedValue { get; set; }
        public string Reason { get; set; }
    }
    internal sealed class ProductProseKocCorrection
    {
        public string TargetKind { get; set; }
        public int EntityId { get; set; }
        public string Sku { get; set; }
        public string LanguageCode { get; set; }
        public string Field { get; set; }
        public string DetectionKind { get; set; }
        public string OldSha256 { get; set; }
        public string NewSha256 { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string OldSentence { get; set; }
        public string ReplacementSpan { get; set; }
        public string CanonicalSentence { get; set; }
        public int OldChoiceSentenceCount { get; set; }
        public int NewCanonicalSentenceCount { get; set; }
        public string HtmlTagSequenceSha256 { get; set; }
        public List<ProductProseAdditionalEdit> AdditionalEdits { get; set; } = new();
        public List<ProductProseAcceptedPreviousValue> AcceptedPreviousValues { get; set; } = new();
    }
    internal sealed class ProductProseAcceptedPreviousValue
    {
        public string Value { get; set; }
        public string Sha256 { get; set; }
    }
    internal sealed class ProductProseAdditionalEdit
    {
        public string OldText { get; set; }
        public string NewText { get; set; }
        public string Reason { get; set; }
    }
    internal sealed class ProductProseSupplementalPackage
    {
        public int SchemaVersion { get; set; }
        public int SlugPolicyVersion { get; set; }
        public string Status { get; set; }
        public string SourceDatabase { get; set; }
        public string SourceMode { get; set; }
        public int RowCount { get; set; }
        public int ProductCount { get; set; }
        public int LanguageCount { get; set; }
        public Dictionary<string, int> FieldCounts { get; set; } = new();
        public int EditCount { get; set; }
        public string TargetTupleSetSha256 { get; set; }
        public List<ProductProseSupplementalCorrection> Rows { get; set; } = new();
        public int SlugCount { get; set; }
        public string SlugTargetSetSha256 { get; set; }
        public List<ProductProseSupplementalSlug> Slugs { get; set; } = new();
        public int WitnessEntryCount { get; set; }
        public int WitnessUniqueTupleCount { get; set; }
        public int WitnessPhraseOccurrenceCount { get; set; }
        public List<ProductProseSupplementalWitness> PreservedWitnesses { get; set; } = new();
        public Dictionary<string, JsonElement> Validation { get; set; } = new();
    }
    internal sealed class ProductProseSupplementalSlug
    {
        public int EntityId { get; set; }
        public string Sku { get; set; }
        public string LanguageCode { get; set; }
        public string SourceField { get; set; }
        public string SourceValueSha256 { get; set; }
        public int SlugPolicyVersion { get; set; }
        public string Slug { get; set; }
        public string SlugBase { get; set; }
        public List<string> PreviousSlugs { get; set; } = new();
    }
    internal sealed class ProductProseSupplementalCorrection
    {
        public int EntityId { get; set; }
        public string Sku { get; set; }
        public string LanguageCode { get; set; }
        public string Field { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string OldSha256 { get; set; }
        public string NewSha256 { get; set; }
        public string TransformationKind { get; set; }
        public string OldHtmlTagSequenceSha256 { get; set; }
        public string NewHtmlTagSequenceSha256 { get; set; }
        public List<ProductProseAdditionalEdit> Edits { get; set; } = new();
    }
    internal sealed class ProductProseSupplementalWitness
    {
        public int EntityId { get; set; }
        public string Sku { get; set; }
        public string LanguageCode { get; set; }
        public string Field { get; set; }
        public string ExpectedValue { get; set; }
        public string ExpectedSha256 { get; set; }
        public string Reason { get; set; }
        public List<ProductProseSupplementalWitnessPhrase> Phrases { get; set; } = new();
    }
    internal sealed class ProductProseSupplementalWitnessPhrase
    {
        public string Phrase { get; set; }
        public int ExpectedOccurrenceCount { get; set; }
    }
    internal sealed class ValidationResult
    {
        public int ErrorCount { get; set; }
    }
    internal sealed class LocalizationQualityGate
    {
        public int SchemaVersion { get; set; }
        public int SlugPolicyVersion { get; set; }
        public int SlugRegenerationErrorCount { get; set; }
        public int ManifestLocalizedEntryCount { get; set; }
        public int ManifestSlugValueLinkCheckCount { get; set; }
        public int ProductTagSlugValueLinkCheckCount { get; set; }
        public int SlugValueLinkCheckCount { get; set; }
        public int NumericOnlySlugCount { get; set; }
        public int NumericLarpCollisionCounterSlugCount { get; set; }
        public int ReviewedLarpSlugMappingCount { get; set; }
        public int LiveUrlRecordOwnerConflictCount { get; set; }
        public int DisavowedForeignProductTagHistoryCount { get; set; }
        public int RetainedPreviousSlugCount { get; set; }
        public int ManifestPreviousSlugCount { get; set; }
        public int ProductTagPreviousSlugCount { get; set; }
        public int DisavowedForeignPreviousSlugCount { get; set; }
        public int LanguageCount { get; set; }
        public int PathologyErrorCount { get; set; }
        public int ProductQualityErrorCount { get; set; }
        public int ProductTagReviewedTagCount { get; set; }
        public int ProductTagLocalizedValueCount { get; set; }
        public int BlogTagReviewedResourceCount { get; set; }
        public int BlogTagReviewedTranslationCount { get; set; }
        public int PublicUiResourceCountPerLanguage { get; set; }
        public int PublicUiLocalizedEntryCount { get; set; }
        public int ContactUiReviewedResourceCount { get; set; }
        public int ContactUiReviewedTranslationCount { get; set; }
        public int AttributeDistinctSourceCount { get; set; }
        public int AttributeLocalizedRowCount { get; set; }
        public int KocNockCheckCount { get; set; }
        public int ProductProseKocCorrectionCount { get; set; }
        public int ProductProseKocCorrectionOccurrenceCount { get; set; }
        public int ProductProseKocCorrectionProductCount { get; set; }
        public int ProductProseKocCorrectionLanguageCount { get; set; }
        public int ProductProseKocHtmlStructureCheckCount { get; set; }
        public int ProductProseKocCorrectionErrorCount { get; set; }
        public int ProductProseSupplementalCorrectionCount { get; set; }
        public int ProductProseSupplementalProductCount { get; set; }
        public int ProductProseSupplementalLanguageCount { get; set; }
        public int ProductProseSupplementalNameCount { get; set; }
        public int ProductProseSupplementalShortDescriptionCount { get; set; }
        public int ProductProseSupplementalFullDescriptionCount { get; set; }
        public int ProductProseSupplementalEditCount { get; set; }
        public int ProductProseSupplementalExactEditRowCount { get; set; }
        public int ProductProseSupplementalFullReplacementCount { get; set; }
        public int ProductProseSupplementalHtmlStructureCheckCount { get; set; }
        public int ProductProseSupplementalHtmlStructureChangeCount { get; set; }
        public int ProductProseSupplementalWitnessEntryCount { get; set; }
        public int ProductProseSupplementalWitnessUniqueTupleCount { get; set; }
        public int ProductProseSupplementalWitnessPhraseOccurrenceCount { get; set; }
        public int ProductProseSupplementalCompoundWitnessCount { get; set; }
        public int ProductProseSupplementalSwordWitnessCount { get; set; }
        public int ProductProseSupplementalStandaloneUrduNoseTargetCount { get; set; }
        public int ProductProseSupplementalIncorrectUrduNockTermTargetCount { get; set; }
        public int ProductProseSupplementalIssueSetOverlapCount { get; set; }
        public string ProductProseSupplementalTargetTupleSetSha256 { get; set; }
        public int ProductProseSupplementalSemanticResidualCount { get; set; }
        public int ProductProseSupplementalErrorCount { get; set; }
        public int ProductProseSupplementalSlugPolicyVersion { get; set; }
        public int ProductProseSupplementalReviewedSlugCount { get; set; }
        public int ProductProseSupplementalSlugSourceNameLinkCount { get; set; }
        public int ProductProseSupplementalPreviousSlugRedirectCount { get; set; }
        public int ProductProseSupplementalUniqueSlugCount { get; set; }
        public int ProductProseSupplementalUniquePreviousSlugCount { get; set; }
        public int ProductProseSupplementalProhibitedSlugTermCount { get; set; }
        public string ProductProseSupplementalSlugTargetSetSha256 { get; set; }
        public int ProductProseSupplementalSlugErrorCount { get; set; }
        public int ProductProseAdditionalEditCount { get; set; }
        public int ProductProseAdditionalEditRowCount { get; set; }
        public int ProductProseAcceptedPreviousValueCount { get; set; }
        public bool FinalProductProseLinguisticIndependentQaDeployable { get; set; }
        public int ProductProseLinguisticRouteCount { get; set; }
        public int ProductProseLinguisticDistinctSentenceCount { get; set; }
        public int ProductProseLinguisticAuthorityReviewCount { get; set; }
        public int ProductProseLinguisticSentenceReviewCount { get; set; }
        public int ProductProseLinguisticErrorCount { get; set; }
        public int UrduNockLinguisticReviewedRowCount { get; set; }
        public int UrduNockLinguisticProductTagReviewedRowCount { get; set; }
        public int UrduNockLinguisticProductAttributeReviewedRowCount { get; set; }
        public int UrduNockLinguisticResidualCount { get; set; }
        public int ReviewedProductProseTranslationCount { get; set; }
        public string ReviewedProseQualityAuditSha256 { get; set; }
        public int ReviewedArcheryCategoryTranslationCount { get; set; }
        public int ReviewedTurkishBowSourceCount { get; set; }
        public int SemanticReviewedCellApplicationCount { get; set; }
        public int ManifestContentQualityErrorCount { get; set; }
        public string ReviewedArcheryCategoryAuditSha256 { get; set; }
        public string ManifestContentQualityAuditSha256 { get; set; }
        public string SemanticReviewApplicationAuditSha256 { get; set; }
        public string ProductProseCanonicalLinguisticIndependentQaSha256 { get; set; }
        public string UrduNockLinguisticCorrectionAuditSha256 { get; set; }
        public int DesiredSlugOwnerConflictCount { get; set; }
        public int ReviewedUrlTransferCount { get; set; }
        public int ManualProductPairReviewCount { get; set; }
        public int ManualContactUiReviewCount { get; set; }
        public int IndependentReviewCorrectionCount { get; set; }
        public int SupplementalPathologyReviewCount { get; set; }
        public int SupplementalIndependentReviewCount { get; set; }
        public int ManualReviewPrimaryFixCount { get; set; }
        public int ManualReviewApplicationErrorCount { get; set; }
        public int ManualReviewAppliedTargetCount { get; set; }
        public bool FinalManifestIndependentQaDeployable { get; set; }
        public bool FinalProductIndependentQaDeployable { get; set; }
        public bool FinalUnchangedLabelIndependentQaDeployable { get; set; }
        public bool FinalLarpSlugLinguisticIndependentQaDeployable { get; set; }
        public int FinalManifestResolvedTupleCount { get; set; }
        public int PublicMetaDescriptionCompletenessCheckCount { get; set; }
        public int FinalProductCorrectionCheckCount { get; set; }
        public int FinalProductLabelCorrectionCheckCount { get; set; }
        public int FinalProductLabelCorrectedRowCount { get; set; }
        public int FinalProductTagCorrectedRowCount { get; set; }
        public int FinalProductAttributeCorrectionGroupCount { get; set; }
        public int FinalProductAttributeCorrectedRowCount { get; set; }
        public int FinalAcceptedLabelReviewedWarningRowCount { get; set; }
        public int FinalProductRemainingReviewedWarningRowCount { get; set; }
        public int OpaqueTypeCodeCheckCount { get; set; }
        public string LiveUrlRecordOwnershipIndependentQaSha256 { get; set; }
        public Dictionary<string, string> PackageSha256 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ReviewReportSha256 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string ManualReviewApplicationSha256 { get; set; }
        public Dictionary<string, string> FinalEvidenceReportSha256 { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
    private sealed record EmbeddedLocalizationPackage(LocalizationQualityGate QualityGate,
        LocalizationManifest Manifest, UiResourcePackage UiResources, ProductTagPackage ProductTags,
        ProductAttributePackage ProductAttributes, ProductProseKocPackage ProductProseKoc,
        ProductProseSupplementalPackage ProductProseSupplemental);
    internal sealed class LocalizedRow
    {
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }
    internal sealed class SlugRow
    {
        public int EntityId { get; set; }
        public int LanguageId { get; set; }
        public string EntityName { get; set; }
        public string Slug { get; set; }
        public IList<string> PreviousSlugs { get; set; } = Array.Empty<string>();
    }
    private sealed record SourceWitness(int EntityId, string Group, string Key, string Source);
    private sealed class PreparedProductProseCorrections
    {
        public List<ProductProseProductAction> ProductActions { get; } = new();
        public List<ProductProseLocalizedAction> LocalizedActions { get; } = new();
    }
    private sealed record ProductProseProductAction(Product Product, string Field, string Value);
    private sealed record ProductProseLocalizedAction(LocalizedProperty Row, string Value);
    private sealed class LocalizedGroupKeyComparer : IEqualityComparer<(string Group, string Key)>
    {
        public bool Equals((string Group, string Key) x, (string Group, string Key) y) =>
            x.Group.Equals(y.Group, StringComparison.OrdinalIgnoreCase) &&
            x.Key.Equals(y.Key, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Group, string Key) obj) =>
            HashCode.Combine(obj.Group.ToLowerInvariant(), obj.Key.ToLowerInvariant());
    }
}
