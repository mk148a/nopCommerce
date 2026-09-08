using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Models;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.StripeBnplCore.Areas.Admin.Controllers;

[Area(AreaNames.ADMIN)]
[AuthorizeAdmin]
[AutoValidateAntiforgeryToken]
public sealed class StripeBnplController : BasePluginController
{
    private const string ConfigureViewPath = "~/Plugins/Misc.StripeBnplCore/Areas/Admin/Views/StripeBnpl/Configure.cshtml";
    private const string MatrixViewPath = "~/Plugins/Misc.StripeBnplCore/Areas/Admin/Views/StripeBnpl/Matrix.cshtml";

    private readonly IBnplCatalogRiskService _catalogRiskService;
    private readonly IBnplProductEligibilityStore _eligibilityStore;
    private readonly ICategoryService _categoryService;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly ISettingService _settingService;
    private readonly IStripeBnplEnvironmentGuard _environmentGuard;
    private readonly IWebHelper _webHelper;

    public StripeBnplController(IBnplCatalogRiskService catalogRiskService,
        IBnplProductEligibilityStore eligibilityStore,
        ICategoryService categoryService,
        INotificationService notificationService,
        IProductService productService,
        ISettingService settingService,
        IStripeBnplEnvironmentGuard environmentGuard,
        IWebHelper webHelper)
    {
        _catalogRiskService = catalogRiskService;
        _eligibilityStore = eligibilityStore;
        _categoryService = categoryService;
        _notificationService = notificationService;
        _productService = productService;
        _settingService = settingService;
        _environmentGuard = environmentGuard;
        _webHelper = webHelper;
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        var settings = await _settingService.LoadSettingAsync<StripeBnplSettings>();
        return View(ConfigureViewPath, PrepareConfigurationModel(settings));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(StripeBnplConfigurationModel model)
    {
        var settings = await _settingService.LoadSettingAsync<StripeBnplSettings>();
        var errors = ValidateConfiguration(model, settings).ToArray();
        if (errors.Length > 0)
        {
            foreach (var error in errors)
                _notificationService.ErrorNotification(error);
            return RedirectToAction(nameof(Configure));
        }

        settings.UseSandbox = model.UseSandbox;
        settings.SandboxDatabaseName = Normalize(model.SandboxDatabaseName, 256);
        settings.SandboxDatabaseServer = Normalize(model.SandboxDatabaseServer, 256);
        settings.LiveDatabaseName = Normalize(model.LiveDatabaseName, 256);
        settings.LiveDatabaseServer = Normalize(model.LiveDatabaseServer, 256);
        settings.StripeAccountCountryIso2 = Normalize(model.StripeAccountCountryIso2, 2)?.ToUpperInvariant();

        settings.TestRestrictedKey = SecretOrExisting(model.TestRestrictedKey, settings.TestRestrictedKey);
        settings.LiveRestrictedKey = SecretOrExisting(model.LiveRestrictedKey, settings.LiveRestrictedKey);
        settings.TestWebhookSecret = SecretOrExisting(model.TestWebhookSecret, settings.TestWebhookSecret);
        settings.LiveWebhookSecret = SecretOrExisting(model.LiveWebhookSecret, settings.LiveWebhookSecret);

        settings.TestKlarnaConfigurationId = Normalize(model.TestKlarnaConfigurationId, 255);
        settings.LiveKlarnaConfigurationId = Normalize(model.LiveKlarnaConfigurationId, 255);
        settings.TestAffirmConfigurationId = Normalize(model.TestAffirmConfigurationId, 255);
        settings.LiveAffirmConfigurationId = Normalize(model.LiveAffirmConfigurationId, 255);
        settings.TestAfterpayConfigurationId = Normalize(model.TestAfterpayConfigurationId, 255);
        settings.LiveAfterpayConfigurationId = Normalize(model.LiveAfterpayConfigurationId, 255);
        settings.TestZipConfigurationId = Normalize(model.TestZipConfigurationId, 255);
        settings.LiveZipConfigurationId = Normalize(model.LiveZipConfigurationId, 255);

        settings.KlarnaApprovalReference = Normalize(model.KlarnaApprovalReference, 500);
        settings.AffirmApprovalReference = Normalize(model.AffirmApprovalReference, 500);
        settings.AfterpayApprovalReference = Normalize(model.AfterpayApprovalReference, 500);
        settings.ZipApprovalReference = Normalize(model.ZipApprovalReference, 500);
        settings.AfterpayMaximumFulfillmentDays = Math.Max(model.AfterpayMaximumFulfillmentDays, 0);
        settings.PendingOrderCancellationHours = model.PendingOrderCancellationHours;
        settings.WebhookSignatureToleranceSeconds = model.WebhookSignatureToleranceSeconds;
        settings.KlarnaMinimumAmount = model.KlarnaMinimumAmount;
        settings.KlarnaMaximumAmount = model.KlarnaMaximumAmount;
        settings.AffirmMinimumAmount = model.AffirmMinimumAmount;
        settings.AffirmMaximumAmount = model.AffirmMaximumAmount;
        settings.AfterpayMinimumAmount = model.AfterpayMinimumAmount;
        settings.AfterpayMaximumAmount = model.AfterpayMaximumAmount;
        settings.ZipMinimumAmount = model.ZipMinimumAmount;
        settings.ZipMaximumAmount = model.ZipMaximumAmount;

        await _settingService.SaveSettingAsync(settings);
        await _settingService.ClearCacheAsync();
        _notificationService.SuccessNotification("Stripe BNPL settings were saved. Payment methods remain fail-closed until every active-mode requirement passes.");
        return RedirectToAction(nameof(Configure));
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Matrix(BnplEligibilityMatrixModel search)
    {
        return View(MatrixViewPath, await PrepareMatrixAsync(search));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> UpdateEligibility(BnplEligibilityUpdateModel model)
    {
        if (!TryGetProvider(model.ProviderId, out var provider) ||
            !TryGetState(model.EligibilityStateId, out var state))
        {
            _notificationService.ErrorNotification("The provider or eligibility state is invalid.");
            return RedirectToMatrix(model);
        }

        var product = await _productService.GetProductByIdAsync(model.ProductId);
        if (product == null || product.Deleted)
        {
            _notificationService.ErrorNotification("The selected product no longer exists.");
            return RedirectToMatrix(model);
        }

        var existing = await _eligibilityStore.GetAsync(product.Id, provider);
        var validationError = await ValidateEligibilityTransitionAsync(product.Id, provider, state,
            model.FulfillmentDays, model.ApprovalReference, existing);
        if (validationError != null)
        {
            _notificationService.ErrorNotification(validationError);
            return RedirectToMatrix(model);
        }

        await _eligibilityStore.UpsertAsync(product.Id, provider, state, model.Reason,
            model.FulfillmentDays, model.ApprovalReference, model.PolicyVersion);
        _notificationService.SuccessNotification($"{product.Name}: {provider} eligibility is now {state}.");
        return RedirectToMatrix(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> BulkUpdateCategory(BnplEligibilityBulkUpdateModel model)
    {
        if (model.CategoryId <= 0 || !TryGetProvider(model.ProviderId, out var provider) ||
            !TryGetState(model.EligibilityStateId, out var state))
        {
            _notificationService.ErrorNotification("A category, provider and valid eligibility state are required.");
            return RedirectToAction(nameof(Matrix), new { providerId = model.ProviderId, categoryId = model.CategoryId });
        }

        var category = await _categoryService.GetCategoryByIdAsync(model.CategoryId);
        if (category == null || category.Deleted)
        {
            _notificationService.ErrorNotification("The selected category no longer exists.");
            return RedirectToAction(nameof(Matrix), new { providerId = model.ProviderId });
        }

        var mappings = await _categoryService.GetProductCategoriesByCategoryIdAsync(category.Id,
            pageSize: int.MaxValue, showHidden: true);
        var productIds = mappings.Select(mapping => mapping.ProductId).Distinct().ToArray();
        var products = (await _productService.GetProductsByIdsAsync(productIds))
            .Where(product => product != null && !product.Deleted)
            .ToList();
        if (products.Count == 0)
        {
            _notificationService.WarningNotification("The selected category contains no products.");
            return RedirectToAction(nameof(Matrix), new { providerId = model.ProviderId, categoryId = model.CategoryId });
        }

        // Validate every product before writing any record so a restricted item
        // cannot produce a partially-applied category operation.
        var existingRecords = (await _eligibilityStore.GetByProductIdsAsync(productIds, provider))
            .GroupBy(record => record.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.UpdatedOnUtc).First());
        foreach (var product in products)
        {
            existingRecords.TryGetValue(product.Id, out var existing);
            var validationError = await ValidateEligibilityTransitionAsync(product.Id, provider, state,
                model.FulfillmentDays, model.ApprovalReference, existing);
            if (validationError != null)
            {
                _notificationService.ErrorNotification($"No products were changed. {product.Name}: {validationError}");
                return RedirectToAction(nameof(Matrix), new { providerId = model.ProviderId, categoryId = model.CategoryId });
            }
        }

        foreach (var product in products)
            await _eligibilityStore.UpsertAsync(product.Id, provider, state, model.Reason,
                model.FulfillmentDays, model.ApprovalReference, model.PolicyVersion);

        _notificationService.SuccessNotification($"Updated {products.Count} products in {category.Name} for {provider}.");
        return RedirectToAction(nameof(Matrix), new { providerId = model.ProviderId, categoryId = model.CategoryId });
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> ExportCsv(int providerId, int categoryId = 0,
        int? eligibilityStateId = null, string searchProductName = null)
    {
        if (!TryGetProvider(providerId, out var provider))
            return BadRequest("Invalid BNPL provider.");

        BnplEligibilityState? stateFilter = null;
        if (eligibilityStateId.HasValue)
        {
            if (!TryGetState(eligibilityStateId.Value, out var parsedState))
                return BadRequest("Invalid eligibility state.");
            stateFilter = parsedState;
        }

        var products = await SearchProductsAsync(categoryId, searchProductName);
        var records = (await _eligibilityStore.GetByProductIdsAsync(products.Select(product => product.Id).ToArray(), provider))
            .GroupBy(record => record.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.UpdatedOnUtc).First());
        if (stateFilter.HasValue)
            products = products.Where(product =>
                (records.TryGetValue(product.Id, out var record) ? record.EligibilityState : BnplEligibilityState.Unknown) == stateFilter.Value).ToList();

        var categories = (await _categoryService.GetAllCategoriesAsync(showHidden: true))
            .Where(category => !category.Deleted)
            .ToDictionary(category => category.Id, category => category.Name);
        var csv = new StringBuilder();
        csv.AppendLine("ProductId,ProductName,SKU,Categories,Provider,EligibilityState,Reason,FulfillmentDays,ApprovalReference,PolicyVersion,RestrictedTerm");
        foreach (var product in products)
        {
            records.TryGetValue(product.Id, out var record);
            var categoryNames = new List<string>();
            foreach (var mapping in await _categoryService.GetProductCategoriesByProductIdAsync(product.Id, showHidden: true))
                if (categories.TryGetValue(mapping.CategoryId, out var name))
                    categoryNames.Add(name);
            var restrictedTerm = await _catalogRiskService.FindRestrictedTermAsync(product.Id);
            AppendCsvRow(csv,
                product.Id.ToString(), product.Name, product.Sku, string.Join(" | ", categoryNames), provider.ToString(),
                (record?.EligibilityState ?? BnplEligibilityState.Unknown).ToString(), record?.Reason,
                record?.FulfillmentDays?.ToString(), record?.ApprovalReference, record?.PolicyVersion, restrictedTerm);
        }

        var bytes = Encoding.UTF8.GetBytes("\uFEFF" + csv);
        var fileName = $"stripe-bnpl-{StripeBnplDefaults.GetProviderSlug(provider)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, MimeTypes.TextCsv, fileName);
    }

    private StripeBnplConfigurationModel PrepareConfigurationModel(StripeBnplSettings settings)
    {
        var environment = _environmentGuard.Check();
        return new StripeBnplConfigurationModel
        {
            UseSandbox = settings.UseSandbox,
            SandboxDatabaseName = settings.SandboxDatabaseName,
            SandboxDatabaseServer = settings.SandboxDatabaseServer,
            LiveDatabaseName = settings.LiveDatabaseName,
            LiveDatabaseServer = settings.LiveDatabaseServer,
            StripeAccountCountryIso2 = settings.StripeAccountCountryIso2,
            TestRestrictedKey = null,
            TestRestrictedKeyConfigured = !string.IsNullOrWhiteSpace(settings.TestRestrictedKey),
            LiveRestrictedKey = null,
            LiveRestrictedKeyConfigured = !string.IsNullOrWhiteSpace(settings.LiveRestrictedKey),
            TestWebhookSecret = null,
            TestWebhookSecretConfigured = !string.IsNullOrWhiteSpace(settings.TestWebhookSecret),
            LiveWebhookSecret = null,
            LiveWebhookSecretConfigured = !string.IsNullOrWhiteSpace(settings.LiveWebhookSecret),
            TestKlarnaConfigurationId = settings.TestKlarnaConfigurationId,
            LiveKlarnaConfigurationId = settings.LiveKlarnaConfigurationId,
            TestAffirmConfigurationId = settings.TestAffirmConfigurationId,
            LiveAffirmConfigurationId = settings.LiveAffirmConfigurationId,
            TestAfterpayConfigurationId = settings.TestAfterpayConfigurationId,
            LiveAfterpayConfigurationId = settings.LiveAfterpayConfigurationId,
            TestZipConfigurationId = settings.TestZipConfigurationId,
            LiveZipConfigurationId = settings.LiveZipConfigurationId,
            KlarnaApprovalReference = settings.KlarnaApprovalReference,
            AffirmApprovalReference = settings.AffirmApprovalReference,
            AfterpayApprovalReference = settings.AfterpayApprovalReference,
            ZipApprovalReference = settings.ZipApprovalReference,
            AfterpayMaximumFulfillmentDays = Math.Max(settings.AfterpayMaximumFulfillmentDays, 0),
            PendingOrderCancellationHours = Math.Clamp(settings.PendingOrderCancellationHours <= 0 ? 24 : settings.PendingOrderCancellationHours, 1, 168),
            WebhookSignatureToleranceSeconds = settings.WebhookSignatureToleranceSeconds,
            KlarnaMinimumAmount = settings.KlarnaMinimumAmount,
            KlarnaMaximumAmount = settings.KlarnaMaximumAmount,
            AffirmMinimumAmount = settings.AffirmMinimumAmount,
            AffirmMaximumAmount = settings.AffirmMaximumAmount,
            AfterpayMinimumAmount = settings.AfterpayMinimumAmount,
            AfterpayMaximumAmount = settings.AfterpayMaximumAmount,
            ZipMinimumAmount = settings.ZipMinimumAmount,
            ZipMaximumAmount = settings.ZipMaximumAmount,
            WebhookUrl = $"{_webHelper.GetStoreLocation().TrimEnd('/')}/{StripeBnplDefaults.WebhookPath}",
            EnvironmentIsSafe = environment.IsSafe,
            EnvironmentStatus = environment.Reason
        };
    }

    private async Task<BnplEligibilityMatrixModel> PrepareMatrixAsync(BnplEligibilityMatrixModel search)
    {
        if (!TryGetProvider(search.ProviderId, out var provider))
            provider = BnplProvider.Klarna;
        var pageSize = Math.Clamp(search.PageSize, 10, 100);
        var pageNumber = Math.Max(1, search.PageNumber);
        var products = await SearchProductsAsync(search.CategoryId, search.SearchProductName);
        var records = (await _eligibilityStore.GetByProductIdsAsync(products.Select(product => product.Id).ToArray(), provider))
            .GroupBy(record => record.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.UpdatedOnUtc).First());

        BnplEligibilityState? stateFilter = null;
        if (search.EligibilityStateId.HasValue && TryGetState(search.EligibilityStateId.Value, out var parsedState))
            stateFilter = parsedState;
        if (stateFilter.HasValue)
            products = products.Where(product =>
                (records.TryGetValue(product.Id, out var record) ? record.EligibilityState : BnplEligibilityState.Unknown) == stateFilter.Value).ToList();

        var totalCount = products.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (decimal)pageSize));
        pageNumber = Math.Min(pageNumber, totalPages);
        var pageProducts = products.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        var rows = new List<BnplEligibilityRowModel>(pageProducts.Count);
        foreach (var product in pageProducts)
        {
            records.TryGetValue(product.Id, out var record);
            var state = record?.EligibilityState ?? BnplEligibilityState.Unknown;
            rows.Add(new BnplEligibilityRowModel
            {
                Id = record?.Id ?? 0,
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                ProviderId = (int)provider,
                EligibilityStateId = (int)state,
                ExistingEligibilityStateId = (int)state,
                Reason = record?.Reason,
                FulfillmentDays = record?.FulfillmentDays,
                ApprovalReference = record?.ApprovalReference,
                PolicyVersion = record?.PolicyVersion,
                RestrictedTerm = await _catalogRiskService.FindRestrictedTermAsync(product.Id)
            });
        }

        var categoryOptions = (await _categoryService.GetAllCategoriesAsync(showHidden: true))
            .Where(category => !category.Deleted)
            .OrderBy(category => category.Name)
            .Select(category => new BnplCategoryOptionModel { Id = category.Id, Name = category.Name })
            .ToArray();
        return new BnplEligibilityMatrixModel
        {
            SearchProductName = search.SearchProductName?.Trim(),
            ProviderId = (int)provider,
            EligibilityStateId = stateFilter.HasValue ? (int)stateFilter.Value : null,
            CategoryId = search.CategoryId,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Categories = categoryOptions,
            Rows = rows
        };
    }

    private async Task<List<Product>> SearchProductsAsync(int categoryId, string keywords)
    {
        var categoryIds = categoryId > 0 ? new List<int> { categoryId } : null;
        var products = await _productService.SearchProductsAsync(pageSize: int.MaxValue,
            categoryIds: categoryIds, keywords: Normalize(keywords, 400), searchSku: true,
            searchDescriptions: false, searchProductTags: true,
            orderBy: ProductSortingEnum.NameAsc, showHidden: true);
        return products.Where(product => !product.Deleted).ToList();
    }

    private async Task<string> ValidateEligibilityTransitionAsync(int productId, BnplProvider provider,
        BnplEligibilityState state, int? fulfillmentDays, string approvalReference,
        BnplProductEligibility existing)
    {
        if (fulfillmentDays is < 0)
            return "Fulfillment days cannot be negative.";

        var settings = await _settingService.LoadSettingAsync<StripeBnplSettings>();
        var afterpayMaximumDays = Math.Max(settings.AfterpayMaximumFulfillmentDays, 0);
        if (provider == BnplProvider.Afterpay && state == BnplEligibilityState.Allowed && afterpayMaximumDays > 0 &&
            (!fulfillmentDays.HasValue || fulfillmentDays.Value > afterpayMaximumDays))
            return $"Afterpay Allowed requires a known fulfillment time of {afterpayMaximumDays} days or less.";

        if (state != BnplEligibilityState.Allowed)
            return null;

        var restrictedTerm = await _catalogRiskService.FindRestrictedTermAsync(productId);
        var wasBlockedByPolicy = existing?.EligibilityState is BnplEligibilityState.ApprovalRequired or BnplEligibilityState.Prohibited;
        if ((restrictedTerm != null || wasBlockedByPolicy) && string.IsNullOrWhiteSpace(approvalReference))
            return restrictedTerm == null
                ? "A written provider/Stripe support reference is required to override the existing restricted state."
                : $"Catalog risk term '{restrictedTerm}' requires a written provider/Stripe support reference before Allowed can be saved.";

        return null;
    }

    private IActionResult RedirectToMatrix(BnplEligibilityUpdateModel model) =>
        RedirectToAction(nameof(Matrix), new
        {
            providerId = model.ProviderId,
            eligibilityStateId = model.EligibilityStateFilterId,
            categoryId = model.CategoryId,
            searchProductName = model.SearchProductName,
            pageNumber = model.PageNumber,
            pageSize = model.PageSize
        });

    private static IEnumerable<string> ValidateConfiguration(StripeBnplConfigurationModel model,
        StripeBnplSettings settings)
    {
        if (string.IsNullOrWhiteSpace(model.StripeAccountCountryIso2) ||
            model.StripeAccountCountryIso2.Trim().Length != 2 ||
            !model.StripeAccountCountryIso2.Trim().All(char.IsLetter))
            yield return "Stripe account country must be a two-letter ISO code.";

        if (model.UseSandbox && (string.IsNullOrWhiteSpace(model.SandboxDatabaseName) || string.IsNullOrWhiteSpace(model.SandboxDatabaseServer)))
            yield return "Sandbox mode requires both the sandbox database name and server allowlist.";
        if (!model.UseSandbox && (string.IsNullOrWhiteSpace(model.LiveDatabaseName) || string.IsNullOrWhiteSpace(model.LiveDatabaseServer)))
            yield return "Live mode requires both the live database name and server allowlist.";

        foreach (var error in ValidatePrefix(model.TestRestrictedKey, "rk_test_", "Test restricted key"))
            yield return error;
        foreach (var error in ValidatePrefix(model.LiveRestrictedKey, "rk_live_", "Live restricted key"))
            yield return error;
        foreach (var error in ValidatePrefix(model.TestWebhookSecret, "whsec_", "Test webhook secret"))
            yield return error;
        foreach (var error in ValidatePrefix(model.LiveWebhookSecret, "whsec_", "Live webhook secret"))
            yield return error;

        var configurationIds = new[]
        {
            (model.TestKlarnaConfigurationId, "Test Klarna configuration"),
            (model.LiveKlarnaConfigurationId, "Live Klarna configuration"),
            (model.TestAffirmConfigurationId, "Test Affirm configuration"),
            (model.LiveAffirmConfigurationId, "Live Affirm configuration"),
            (model.TestAfterpayConfigurationId, "Test Afterpay configuration"),
            (model.LiveAfterpayConfigurationId, "Live Afterpay configuration"),
            (model.TestZipConfigurationId, "Test Zip configuration"),
            (model.LiveZipConfigurationId, "Live Zip configuration")
        };
        foreach (var (value, label) in configurationIds)
            foreach (var error in ValidatePrefix(value, "pmc_", label))
                yield return error;

        var testKey = SecretOrExisting(model.TestRestrictedKey, settings.TestRestrictedKey);
        var liveKey = SecretOrExisting(model.LiveRestrictedKey, settings.LiveRestrictedKey);
        if (!string.IsNullOrWhiteSpace(testKey) && string.Equals(testKey, liveKey, StringComparison.Ordinal))
            yield return "Test and live restricted keys must be different.";
        var testWebhook = SecretOrExisting(model.TestWebhookSecret, settings.TestWebhookSecret);
        var liveWebhook = SecretOrExisting(model.LiveWebhookSecret, settings.LiveWebhookSecret);
        if (!string.IsNullOrWhiteSpace(testWebhook) && string.Equals(testWebhook, liveWebhook, StringComparison.Ordinal))
            yield return "Test and live webhook secrets must be different.";

        if (model.AfterpayMaximumFulfillmentDays < 0)
            yield return "Afterpay maximum fulfillment days cannot be negative.";
        if (model.PendingOrderCancellationHours is < 1 or > 168)
            yield return "Pending BNPL order cancellation hours must be between 1 and 168.";
        if (model.WebhookSignatureToleranceSeconds is < 60 or > 600)
            yield return "Webhook signature tolerance must be between 60 and 600 seconds.";

        foreach (var error in ValidateAmountRange(model.KlarnaMinimumAmount, model.KlarnaMaximumAmount, "Klarna"))
            yield return error;
        foreach (var error in ValidateAmountRange(model.AffirmMinimumAmount, model.AffirmMaximumAmount, "Affirm"))
            yield return error;
        foreach (var error in ValidateAmountRange(model.AfterpayMinimumAmount, model.AfterpayMaximumAmount, "Afterpay"))
            yield return error;
        foreach (var error in ValidateAmountRange(model.ZipMinimumAmount, model.ZipMaximumAmount, "Zip"))
            yield return error;
    }

    private static IEnumerable<string> ValidatePrefix(string value, string prefix, string label)
    {
        if (!string.IsNullOrWhiteSpace(value) && !value.Trim().StartsWith(prefix, StringComparison.Ordinal))
            yield return $"{label} must start with '{prefix}'.";
    }

    private static IEnumerable<string> ValidateAmountRange(decimal minimum, decimal maximum, string provider)
    {
        if (minimum < 0 || maximum <= 0 || maximum < minimum)
            yield return $"{provider} amount range is invalid.";
    }

    private static bool TryGetProvider(int value, out BnplProvider provider)
    {
        provider = (BnplProvider)value;
        return Enum.IsDefined(provider);
    }

    private static bool TryGetState(int value, out BnplEligibilityState state)
    {
        state = (BnplEligibilityState)value;
        return Enum.IsDefined(state);
    }

    private static string SecretOrExisting(string postedValue, string existingValue) =>
        string.IsNullOrWhiteSpace(postedValue) ? existingValue : postedValue.Trim();

    private static string Normalize(string value, int maximumLength)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private static void AppendCsvRow(StringBuilder output, params string[] values) =>
        output.AppendLine(string.Join(',', values.Select(EscapeCsv)));

    private static string EscapeCsv(string value)
    {
        value = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t')
            value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
