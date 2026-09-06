using System.Security.Cryptography;
using System.Text;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Seo;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class LocalizationResourceInstallerGuardTests
{
    [Test]
    public void FinalCoverageContractIsAccepted()
    {
        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateCoverageContract(
            1168, 1120, 819 * 24, 1120 + 819 * 24,
            1168, 1120, 819 * 24, 2));
    }

    [TestCase(1154, 1120 + 819 * 24, 2)]
    [TestCase(1168, 1106 + 819 * 24, 2)]
    [TestCase(1168, 1120 + 819 * 24, 1)]
    public void StaleCoverageOrProductAttributeSchemaIsRejected(int manifestEntryCount,
        int slugValueLinkCheckCount, int productAttributeSchemaVersion)
    {
        Assert.That(() => LocalizationResourceInstaller.ValidateCoverageContract(
                manifestEntryCount, 1120, 819 * 24, slugValueLinkCheckCount,
                1168, 1120, 819 * 24, productAttributeSchemaVersion),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("coverage contract"));
    }

    [Test]
    public void SameProductIdWithChangedIdentityIsRejected()
    {
        var product = new Product
        {
            Id = 339,
            Sku = "horsearmor6",
            Name = "Elven Horse Armor",
            ShortDescription = "Handmade armor",
            FullDescription = "Full source description",
            MetaTitle = "Elven Horse Armor",
            MetaDescription = "Handmade leather horse armor.",
            MetaKeywords = "horse armor"
        };
        var witness = new LocalizationResourceInstaller.ManifestEntry
        {
            EntityType = nameof(Product),
            EntityId = product.Id,
            Sku = product.Sku,
            SourceHash = SourceHash(product.Name, product.ShortDescription, product.FullDescription,
                product.MetaTitle, product.MetaDescription, product.MetaKeywords)
        };

        LocalizationResourceInstaller.ValidateProductIdentityWitness(witness, product);

        product.Name = "Unrelated product that reused id 339";
        Assert.That(() => LocalizationResourceInstaller.ValidateProductIdentityWitness(witness, product),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("source hash"));

        product.Name = "Elven Horse Armor";
        product.Sku = "different-sku";
        Assert.That(() => LocalizationResourceInstaller.ValidateProductIdentityWitness(witness, product),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("identity mismatch"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ForeignActiveOrHistoricalSlugOwnerRequiresExactReview(bool isActive)
    {
        var desired = new List<LocalizationResourceInstaller.SlugRow>
        {
            new() { EntityName = "Category", EntityId = 99, LanguageId = 9, Slug = "replacement" },
            new() { EntityName = "Category", EntityId = 48, LanguageId = 9, Slug = "shared-slug" }
        };
        var historicalOwner = new UrlRecord
        {
            Id = 700,
            EntityName = "Category",
            EntityId = 99,
            LanguageId = 9,
            Slug = "shared-slug",
            IsActive = isActive
        };

        Assert.That(() => LocalizationResourceInstaller.ValidateDesiredSlugOwnership(
                desired, new List<UrlRecord> { historicalOwner }, new HashSet<int>()),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("unreviewed historical owner"));

        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateDesiredSlugOwnership(
            desired, new List<UrlRecord> { historicalOwner }, new HashSet<int> { historicalOwner.Id }));
    }

    [Test]
    public void ReviewedSlugTransferMustMatchEveryPersistedIdentityField()
    {
        var current = new UrlRecord
        {
            Id = 708,
            EntityName = "ProductTag",
            EntityId = 443,
            LanguageId = 0,
            Slug = "kleidung-mittelalter",
            IsActive = true
        };
        var reviewed = new LocalizationResourceInstaller.RetiredUrlRecord
        {
            Id = current.Id,
            EntityName = current.EntityName,
            EntityId = current.EntityId,
            LanguageId = current.LanguageId,
            Slug = current.Slug
        };

        LocalizationResourceInstaller.ValidateRetiredUrlRecords(
            new List<LocalizationResourceInstaller.RetiredUrlRecord> { reviewed },
            new List<UrlRecord> { current });

        reviewed.EntityId++;
        Assert.That(() => LocalizationResourceInstaller.ValidateRetiredUrlRecords(
                new List<LocalizationResourceInstaller.RetiredUrlRecord> { reviewed },
                new List<UrlRecord> { current }),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("no longer matches"));
    }

    [Test]
    public void EmbeddedPackagePassesPositivePreflightContract()
    {
        Assert.DoesNotThrow(LocalizationResourceInstaller.ValidateEmbeddedPackagePreflight);
    }

    [Test]
    public void EmbeddedKocBrandAuthorityPassesExact24RouteContract()
    {
        var (authority, productTags) = LocalizationResourceInstaller
            .ReadEmbeddedProductTagKocBrandAuthorityValidationFixture();

        Assert.DoesNotThrow(() => LocalizationResourceInstaller
            .ValidateProductTagKocBrandAuthority(authority, productTags, (_, _) => true));
        Assert.Multiple(() =>
        {
            Assert.That(authority.Routes, Has.Count.EqualTo(24));
            Assert.That(authority.Summary.LabelChangeCount, Is.EqualTo(16));
            Assert.That(authority.Summary.SlugChangeCount, Is.EqualTo(13));
            Assert.That(authority.Routes.Single(row => row.LanguageCode == "tr").TargetValue,
                Is.EqualTo("Koç gez"));
            Assert.That(authority.Routes.Where(row => row.LanguageCode != "tr")
                .All(row => row.TargetValue == "Koç Nock" && row.TargetSlug == "koc-nock"),
                Is.True);
        });
    }

    [TestCase("policy")]
    [TestCase("target-value")]
    [TestCase("package-value")]
    [TestCase("package-slug")]
    [TestCase("legacy-history")]
    [TestCase("summary-count")]
    [TestCase("tuple-hash")]
    [TestCase("binding-hash")]
    public void KocBrandAuthorityRejectsEveryExactBoundary(string mutation)
    {
        var (authority, productTags) = LocalizationResourceInstaller
            .ReadEmbeddedProductTagKocBrandAuthorityValidationFixture();
        Func<string, string, bool> matchesHash = (_, _) => true;
        switch (mutation)
        {
            case "policy":
                authority.Policy.GenericProseTermsRemainLocalized = false;
                break;
            case "target-value":
                authority.Routes.Single(row => row.LanguageCode == "de").TargetValue = "Koç-Nocke";
                break;
            case "package-value":
                productTags.Values.Single(row => row.EntityId == 583 && row.LanguageCode == "de")
                    .Value = "Koç-Nocke";
                break;
            case "package-slug":
                productTags.Slugs.Single(row => row.EntityId == 583 && row.LanguageCode == "ru")
                    .Slug = "hvvostovik-koc";
                break;
            case "legacy-history":
            {
                var route = authority.Routes.Single(row => row.LanguageCode == "ru");
                productTags.Slugs.Single(row => row.EntityId == 583 && row.LanguageCode == "ru")
                    .PreviousSlugs.RemoveAll(slug => slug.Equals(route.RequiredPreviousSlug,
                        StringComparison.OrdinalIgnoreCase));
                break;
            }
            case "summary-count":
                authority.Summary.LabelChangeCount--;
                break;
            case "tuple-hash":
                authority.TargetTupleSetSha256 = new string('A', 64);
                break;
            case "binding-hash":
                matchesHash = (_, _) => false;
                break;
            default:
                Assert.Fail($"Unknown mutation '{mutation}'.");
                break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateProductTagKocBrandAuthority(
                authority, productTags, matchesHash),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void EmbeddedSupplementalProductProsePassesExactContract()
    {
        var package = LocalizationResourceInstaller
            .ReadEmbeddedProductProseSupplementalValidationFixture();

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseSupplementalPackage(package));
    }

    [Test]
    public void EmbeddedSupplementalProduct277UsesExactReviewedLiveTransition()
    {
        var package = LocalizationResourceInstaller
            .ReadEmbeddedProductProseSupplementalValidationFixture();
        var row = package.Rows.Single(item => item.EntityId == 277 &&
            item.LanguageCode == "ur" && item.Field == "FullDescription");

        Assert.Multiple(() =>
        {
            Assert.That(package.SourceDatabase,
                Is.EqualTo("HoodArcheryShopV480bugfixLancelotDb"));
            Assert.That(row.OldSha256, Is.EqualTo(
                "E7F75A63CDE15BF3C8A52811445BA77E959EA2BEC29CF0A2E198D7F0CA880DE5"));
            Assert.That(row.NewSha256, Is.EqualTo(
                "9C13FC47E41A1428E1575AE697B865905F8A9F707B827C547AA2415D093130F2"));
            Assert.That(row.OldValue, Has.Length.EqualTo(56504));
            Assert.That(row.NewValue, Has.Length.EqualTo(56514));
        });

        Assert.That(LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
            row.OldValue, row), Is.EqualTo(row.NewValue));
        Assert.That(LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
            row.NewValue, row), Is.SameAs(row.NewValue));
        Assert.That(() => LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
                row.OldValue + " merchant drift", row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("drifted"));
    }

    [TestCase("row-count")]
    [TestCase("product-count")]
    [TestCase("language-count")]
    [TestCase("field-count")]
    [TestCase("edit-count")]
    [TestCase("tuple-hash")]
    [TestCase("row-sku")]
    [TestCase("row-language")]
    [TestCase("duplicate-tuple")]
    [TestCase("old-hash")]
    [TestCase("new-hash")]
    [TestCase("html-hash")]
    [TestCase("unknown-transformation")]
    [TestCase("second-full-replacement")]
    [TestCase("witness-entry-count")]
    [TestCase("witness-tuple-count")]
    [TestCase("witness-occurrence-count")]
    [TestCase("witness-hash")]
    [TestCase("witness-blank-reason")]
    [TestCase("witness-phrase-count")]
    [TestCase("witness-duplicate-phrase")]
    [TestCase("schema-version")]
    [TestCase("source-database")]
    [TestCase("slug-package-policy")]
    [TestCase("slug-count")]
    [TestCase("slug-target-hash")]
    [TestCase("slug-source-field")]
    [TestCase("slug-source-hash")]
    [TestCase("slug-policy")]
    [TestCase("slug-duplicate")]
    [TestCase("slug-base")]
    [TestCase("slug-previous")]
    [TestCase("slug-prohibited-term")]
    public void SupplementalProductProseRejectsCoverageIdentityTransformationAndWitnessDrift(
        string mutation)
    {
        var package = LocalizationResourceInstaller
            .ReadEmbeddedProductProseSupplementalValidationFixture();
        var exact = package.Rows.First(row => row.TransformationKind == "exactEdits");
        switch (mutation)
        {
            case "row-count": package.RowCount--; break;
            case "product-count": package.ProductCount--; break;
            case "language-count": package.LanguageCount++; break;
            case "field-count": package.FieldCounts["Name"]--; break;
            case "edit-count": package.EditCount--; break;
            case "tuple-hash": package.TargetTupleSetSha256 = new string('A', 64); break;
            case "row-sku": exact.Sku += "-drift"; break;
            case "row-language": exact.LanguageCode = "en"; break;
            case "duplicate-tuple": package.Rows[1] = package.Rows[0]; break;
            case "old-hash": exact.OldSha256 = new string('A', 64); break;
            case "new-hash": exact.NewSha256 = new string('B', 64); break;
            case "html-hash": exact.NewHtmlTagSequenceSha256 = new string('C', 64); break;
            case "unknown-transformation": exact.TransformationKind = "replaceAll"; break;
            case "second-full-replacement":
                exact.TransformationKind = "reviewedFullReplacement";
                exact.Edits.Clear();
                break;
            case "witness-entry-count": package.WitnessEntryCount--; break;
            case "witness-tuple-count": package.WitnessUniqueTupleCount++; break;
            case "witness-occurrence-count": package.WitnessPhraseOccurrenceCount--; break;
            case "witness-hash":
                package.PreservedWitnesses[0].ExpectedSha256 = new string('D', 64);
                break;
            case "witness-blank-reason": package.PreservedWitnesses[0].Reason = " "; break;
            case "witness-phrase-count":
                package.PreservedWitnesses[0].Phrases[0].ExpectedOccurrenceCount++;
                break;
            case "witness-duplicate-phrase":
                package.PreservedWitnesses[1] = package.PreservedWitnesses[0];
                break;
            case "schema-version": package.SchemaVersion--; break;
            case "source-database": package.SourceDatabase = "unreviewed-source"; break;
            case "slug-package-policy": package.SlugPolicyVersion++; break;
            case "slug-count": package.SlugCount--; break;
            case "slug-target-hash": package.SlugTargetSetSha256 = new string('A', 64); break;
            case "slug-source-field": package.Slugs[0].SourceField = "ShortDescription"; break;
            case "slug-source-hash": package.Slugs[0].SourceValueSha256 = new string('A', 64); break;
            case "slug-policy": package.Slugs[0].SlugPolicyVersion++; break;
            case "slug-duplicate": package.Slugs[1].Slug = package.Slugs[0].Slug; break;
            case "slug-base": package.Slugs[0].SlugBase += "-drift"; break;
            case "slug-previous": package.Slugs[0].PreviousSlugs.Clear(); break;
            case "slug-prohibited-term": package.Slugs[0].Slug += "-ناک"; break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.That(() =>
                LocalizationResourceInstaller.ValidateProductProseSupplementalPackage(package),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void SupplementalProductSlugsAreNameLinkedAndMergeIntoTheAtomicSlugSet()
    {
        var package = LocalizationResourceInstaller
            .ReadEmbeddedProductProseSupplementalValidationFixture();
        var slugs = new List<LocalizationResourceInstaller.SlugRow>
        {
            new()
            {
                EntityName = "Category", EntityId = 48, LanguageId = 5,
                Slug = "existing-category"
            }
        };
        var languageIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["ur"] = 5 };

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateSupplementalProductSlugs(package));
        LocalizationResourceInstaller.MergeSupplementalProductSlugs(slugs, package,
            languageIds);

        Assert.That(slugs.Count, Is.EqualTo(9));
        Assert.That(slugs.Count(row => row.EntityName == nameof(Product)), Is.EqualTo(8));
        Assert.That(slugs.Where(row => row.EntityName == nameof(Product))
            .All(row => row.LanguageId == 5 && row.PreviousSlugs.Count == 1), Is.True);
        Assert.That(() => LocalizationResourceInstaller.MergeSupplementalProductSlugs(
                slugs, package, languageIds),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("overlaps"));
    }

    [Test]
    public void SupplementalExactEditsReconstructOnlyTheReviewedNewValue()
    {
        var row = CreateSupplementalCorrection("<p>Old component.</p>",
            "<p>Reviewed component.</p>");

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateSupplementalExactEditTransformation(row));

        row.NewValue = "<p>Unreviewed rewrite.</p>";
        Assert.That(() =>
                LocalizationResourceInstaller.ValidateSupplementalExactEditTransformation(row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("reviewed exact edits"));
    }

    [Test]
    public void SupplementalCorrectionIsOldToNewIdempotentAndRejectsThirdState()
    {
        var row = CreateSupplementalCorrection("old", "new");

        Assert.That(LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
            row.OldValue, row), Is.EqualTo(row.NewValue));
        Assert.That(LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
            row.NewValue, row), Is.SameAs(row.NewValue));
        Assert.That(() =>
                LocalizationResourceInstaller.ResolveSupplementalProductProseCorrectionValue(
                    "merchant-edited third state", row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("drifted"));
    }

    [Test]
    public void SupplementalTupleMayNotOverlapManifestOrMainProductProse()
    {
        var package = new LocalizationResourceInstaller.ProductProseSupplementalPackage
        {
            Rows = new List<LocalizationResourceInstaller.ProductProseSupplementalCorrection>
            {
                new() { EntityId = 17, LanguageCode = "ur", Field = "Name" }
            }
        };
        var languageIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["ur"] = 24 };
        var regular = new List<LocalizationResourceInstaller.LocalizedRow>
        {
            new() { EntityId = 17, LanguageId = 24, Group = "Product", Key = "Name" }
        };

        Assert.That(() => LocalizationResourceInstaller
                .ValidateNoSupplementalProductProseTupleOverlap(regular,
                    new List<LocalizationResourceInstaller.ProductProseKocCorrection>(), package,
                    languageIds),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("overlaps"));

        regular.Clear();
        var main = new List<LocalizationResourceInstaller.ProductProseKocCorrection>
        {
            new()
            {
                TargetKind = "LocalizedProperty", EntityId = 17, LanguageCode = "ur",
                Field = "Name"
            }
        };
        Assert.That(() => LocalizationResourceInstaller
                .ValidateNoSupplementalProductProseTupleOverlap(regular, main, package, languageIds),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("overlaps"));
    }

    [TestCase("wrong-row-sku")]
    [TestCase("wrong-row-language")]
    [TestCase("duplicate-row-tuple")]
    [TestCase("html-sequence-drift")]
    [TestCase("actual-detection-counter-drift")]
    [TestCase("declared-language-counter-drift")]
    [TestCase("false-positive-duplicate")]
    [TestCase("false-positive-language")]
    [TestCase("false-positive-blank-reason")]
    [TestCase("additional-edit-missing")]
    [TestCase("additional-edit-blank-reason")]
    [TestCase("additional-edit-duplicate")]
    [TestCase("additional-edit-count")]
    [TestCase("additional-edit-row-count")]
    public void ProductProsePackageRejectsCounterIdentityAndWitnessDrift(string mutation)
    {
        var (package, productTags) =
            LocalizationResourceInstaller.ReadEmbeddedProductProseKocValidationFixture();
        switch (mutation)
        {
            case "wrong-row-sku":
                package.Rows[0].Sku += "-drift";
                break;
            case "wrong-row-language":
                package.Rows.First(row => row.TargetKind == "LocalizedProperty").LanguageCode = "zz";
                break;
            case "duplicate-row-tuple":
                package.Rows[1] = package.Rows[0];
                break;
            case "html-sequence-drift":
                package.Rows[0].HtmlTagSequenceSha256 = new string('A', 64);
                break;
            case "actual-detection-counter-drift":
                package.Rows[0].DetectionKind = package.Rows[0].DetectionKind == "literalRamNock"
                    ? "naturalSentenceNormalization"
                    : "literalRamNock";
                break;
            case "declared-language-counter-drift":
                package.PerLanguageTargetCounts["en"]--;
                break;
            case "false-positive-duplicate":
                package.PreservedFalsePositives[1] = package.PreservedFalsePositives[0];
                break;
            case "false-positive-language":
                package.PreservedFalsePositives[0].LanguageCode = "en";
                break;
            case "false-positive-blank-reason":
                package.PreservedFalsePositives[0].Reason = " ";
                break;
            case "additional-edit-missing":
                package.Rows.First(row => row.AdditionalEdits.Count > 0).AdditionalEdits.Clear();
                break;
            case "additional-edit-blank-reason":
                package.Rows.First(row => row.AdditionalEdits.Count > 0)
                    .AdditionalEdits[0].Reason = " ";
                break;
            case "additional-edit-duplicate":
            {
                var row = package.Rows.First(item => item.AdditionalEdits.Count > 0);
                row.AdditionalEdits.Add(row.AdditionalEdits[0]);
                break;
            }
            case "additional-edit-count":
                package.AdditionalEditCount--;
                break;
            case "additional-edit-row-count":
                package.AdditionalEditRowCount--;
                break;
            default:
                Assert.Fail($"Unknown mutation '{mutation}'.");
                break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateProductProseKocPackage(
                package, productTags),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void ProductProseCorrectionMovesExactOldStateToReviewedNewState()
    {
        var row = CreateProductProseCorrection(
            "<p>And tell me your preferred nock type ram nock type or standard.</p>",
            "<p>Please tell me whether you prefer a Koç Nock or a standard nock.</p>");

        var actual = LocalizationResourceInstaller.ResolveProductProseCorrectionValue(row.OldValue, row);

        Assert.That(actual, Is.EqualTo(row.NewValue));
    }

    [Test]
    public void ProductProseCorrectionIsIdempotentAtExactNewState()
    {
        var row = CreateProductProseCorrection(
            "<p>And tell me your preferred nock type ram nock type or standard.</p>",
            "<p>Please tell me whether you prefer a Koç Nock or a standard nock.</p>");

        var actual = LocalizationResourceInstaller.ResolveProductProseCorrectionValue(row.NewValue, row);

        Assert.That(actual, Is.SameAs(row.NewValue));
    }

    [Test]
    public void ProductProseCorrectionRejectsThirdStateWithoutBlindOverwrite()
    {
        var row = CreateProductProseCorrection("old", "new");

        Assert.That(() => LocalizationResourceInstaller.ResolveProductProseCorrectionValue(
                "merchant-edited third state", row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("drifted"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ProductProseCorrectionRejectsPayloadHashDrift(bool corruptOldHash)
    {
        var row = CreateProductProseCorrection("old", "new");
        if (corruptOldHash)
            row.OldSha256 = new string('A', 64);
        else
            row.NewSha256 = new string('B', 64);

        var current = corruptOldHash ? row.OldValue : row.NewValue;
        Assert.That(() => LocalizationResourceInstaller.ResolveProductProseCorrectionValue(current, row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("drifted"));
    }

    [Test]
    public void ReviewedMainProductProsePreviousStateCanAdvanceWithoutWeakeningThirdStateGuard()
    {
        var (package, _) =
            LocalizationResourceInstaller.ReadEmbeddedProductProseKocValidationFixture();
        var row = package.Rows.Single(item => item.TargetKind == "LocalizedProperty" &&
            item.EntityId == 169 && item.Sku == "arrow34" && item.LanguageCode == "ur" &&
            item.Field == "FullDescription");
        var previousValue = row.AcceptedPreviousValues.Count == 1
            ? row.AcceptedPreviousValues[0].Value
            : row.NewValue;
        var finalValue = row.AcceptedPreviousValues.Count == 1
            ? row.NewValue
            : previousValue.Replace("<p>Nocks: Batur nocks (Castle nocks)</p>",
                "<p>Nock: Batur Nock (Castle Nock)</p>", StringComparison.Ordinal);
        row.NewValue = finalValue;
        row.NewSha256 = Sha256(finalValue);
        row.AcceptedPreviousValues = new List<
            LocalizationResourceInstaller.ProductProseAcceptedPreviousValue>
        {
            new() { Value = previousValue, Sha256 = Sha256(previousValue) }
        };

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseAcceptedPreviousValues(row));
        Assert.That(LocalizationResourceInstaller.ResolveProductProseCorrectionValue(
            previousValue, row), Is.EqualTo(finalValue));
        Assert.That(() => LocalizationResourceInstaller.ResolveProductProseCorrectionValue(
                "unreviewed intermediate state", row),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("drifted"));
    }

    [TestCase("wrong-tuple")]
    [TestCase("wrong-hash")]
    [TestCase("duplicate")]
    public void MainProductProseAcceptedPreviousStateIsNarrowlyAllowlisted(string mutation)
    {
        var (package, _) =
            LocalizationResourceInstaller.ReadEmbeddedProductProseKocValidationFixture();
        var row = package.Rows.Single(item => item.TargetKind == "LocalizedProperty" &&
            item.EntityId == 169 && item.LanguageCode == "ur" &&
            item.Field == "FullDescription");
        var previousValue = row.AcceptedPreviousValues.Count == 1
            ? row.AcceptedPreviousValues[0].Value
            : row.NewValue;
        row.NewValue += " reviewed-final";
        row.NewSha256 = Sha256(row.NewValue);
        row.AcceptedPreviousValues = new List<
            LocalizationResourceInstaller.ProductProseAcceptedPreviousValue>
        {
            new() { Value = previousValue, Sha256 = Sha256(previousValue) }
        };
        switch (mutation)
        {
            case "wrong-tuple": row.EntityId++; break;
            case "wrong-hash": row.AcceptedPreviousValues[0].Sha256 = new string('A', 64); break;
            case "duplicate": row.AcceptedPreviousValues.Add(row.AcceptedPreviousValues[0]); break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.That(() =>
                LocalizationResourceInstaller.ValidateProductProseAcceptedPreviousValues(row),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void ProductProseTransformationMayChangeOnlyTheReviewedSentence()
    {
        const string oldSentence = "And tell me your preferred nock type ram nock type or standard.";
        const string canonical = "Please tell me whether you prefer a Koç Nock or a standard nock.";
        var row = CreateProductProseCorrection($"<p>Before. {oldSentence} After.</p>",
            $"<p>Before. {canonical} After.</p>");
        row.OldSentence = oldSentence;
        row.ReplacementSpan = canonical;
        row.CanonicalSentence = canonical;

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseCorrectionTransformation(row, canonical));

        row.NewValue = $"<p>Unreviewed edit. {canonical} After.</p>";
        Assert.That(() =>
                LocalizationResourceInstaller.ValidateProductProseCorrectionTransformation(row, canonical),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("more than"));
    }

    [Test]
    public void ProductProseTransformationAppliesReviewedAdditionalEditsSequentially()
    {
        var row = CreateAdditionalEditCorrection();

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseCorrectionTransformation(
                row, row.CanonicalSentence));
    }

    [TestCase("blank-old")]
    [TestCase("blank-new")]
    [TestCase("blank-reason")]
    [TestCase("duplicate")]
    [TestCase("overlap")]
    [TestCase("canonical-overlap")]
    [TestCase("ambiguous-old")]
    [TestCase("preexisting-new")]
    [TestCase("wrong-final")]
    public void ProductProseTransformationRejectsUnreviewedAdditionalEditDrift(string mutation)
    {
        var row = CreateAdditionalEditCorrection();
        switch (mutation)
        {
            case "blank-old": row.AdditionalEdits[0].OldText = " "; break;
            case "blank-new": row.AdditionalEdits[0].NewText = " "; break;
            case "blank-reason": row.AdditionalEdits[0].Reason = " "; break;
            case "duplicate": row.AdditionalEdits.Add(row.AdditionalEdits[0]); break;
            case "overlap":
                row.OldValue = $"<p>{row.OldSentence} Batur ناک.</p>";
                row.NewValue = $"<p>{row.CanonicalSentence} Batur Nock.</p>";
                row.AdditionalEdits.RemoveAt(1);
                row.AdditionalEdits.Add(new LocalizationResourceInstaller.ProductProseAdditionalEdit
                {
                    OldText = "ناک", NewText = "Nock", Reason = "overlapping review"
                });
                break;
            case "canonical-overlap":
                row.AdditionalEdits.Clear();
                row.AdditionalEdits.Add(new LocalizationResourceInstaller.ProductProseAdditionalEdit
                {
                    OldText = "Koç Nock", NewText = "Koç gez", Reason = "overlapping review"
                });
                break;
            case "ambiguous-old":
                row.OldValue = row.OldValue.Replace("Batur ناک", "Batur ناک and Batur ناک",
                    StringComparison.Ordinal);
                break;
            case "preexisting-new":
                row.OldValue = row.OldValue.Replace("Batur ناک", "Batur ناک and Batur Nock",
                    StringComparison.Ordinal);
                break;
            case "wrong-final": row.NewValue += " Unreviewed."; break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateProductProseCorrectionTransformation(
                row, row.CanonicalSentence),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void EmbeddedCanonicalLinguisticQaPassesExactContract()
    {
        var (qa, prose, productTags) =
            LocalizationResourceInstaller.ReadEmbeddedProductProseLinguisticQaValidationFixture();

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseCanonicalLinguisticQa(
                qa, prose, productTags, (_, _) => true));
    }

    [TestCase("source-binding")]
    [TestCase("authority-binding")]
    [TestCase("supporting-binding")]
    [TestCase("duplicate-route")]
    [TestCase("english-route-group")]
    [TestCase("review-count")]
    [TestCase("authority-term")]
    [TestCase("canonical-sentence")]
    [TestCase("sentence-hash")]
    [TestCase("sentence-nfc")]
    [TestCase("duplicate-sentence")]
    [TestCase("extra-koc")]
    [TestCase("failed-check")]
    [TestCase("failed-verdict")]
    [TestCase("prohibited-term")]
    [TestCase("product-tag-authority")]
    [TestCase("prose-canonical")]
    [TestCase("deployable")]
    [TestCase("error-count")]
    [TestCase("release-decision")]
    public void CanonicalLinguisticQaRejectsEveryBindingAndReviewBoundary(string mutation)
    {
        var (qa, prose, productTags) =
            LocalizationResourceInstaller.ReadEmbeddedProductProseLinguisticQaValidationFixture();
        Func<string, string, bool> matchesHash = (_, _) => true;
        var review = qa.Reviews.Single(item => item.ReviewKey == "tr");
        switch (mutation)
        {
            case "source-binding":
                matchesHash = (file, _) => file != "product-prose-koc-corrections.json";
                break;
            case "authority-binding":
                matchesHash = (file, _) => file != "product-tag-localization.json";
                break;
            case "supporting-binding":
                matchesHash = (file, _) => file != "urdu-nock-linguistic-correction-audit.json";
                break;
            case "duplicate-route": review.RouteCodes[0] = "au"; break;
            case "english-route-group": qa.Reviews.Single(item => item.ReviewKey == "en")
                .RouteCodes.Reverse(); break;
            case "review-count": qa.Reviews.RemoveAt(0); break;
            case "authority-term": review.AuthorityTerm += " drift"; break;
            case "canonical-sentence":
                review.CanonicalSentence += " drift";
                review.CanonicalSentenceSha256 = Sha256(review.CanonicalSentence);
                break;
            case "sentence-hash": review.CanonicalSentenceSha256 = new string('A', 64); break;
            case "sentence-nfc":
                review.CanonicalSentence = review.CanonicalSentence.Normalize(NormalizationForm.FormD);
                review.CanonicalSentenceSha256 = Sha256(review.CanonicalSentence);
                break;
            case "duplicate-sentence":
                review.CanonicalSentence = qa.Reviews[0].CanonicalSentence;
                review.CanonicalSentenceSha256 = Sha256(review.CanonicalSentence);
                break;
            case "extra-koc":
                review.CanonicalSentence += " Koç";
                review.CanonicalSentenceSha256 = Sha256(review.CanonicalSentence);
                break;
            case "failed-check": review.Checks["nativeGrammarNatural"] = false; break;
            case "failed-verdict": review.Verdict = "RED"; break;
            case "prohibited-term": review.ProhibitedAnimalOrNonArcheryTerms.Add("Koç"); break;
            case "product-tag-authority":
                productTags.Values.Single(item => item.EntityId == 583 && item.LanguageCode == "tr")
                    .Value += " drift";
                break;
            case "prose-canonical": prose.CanonicalSentencesByLanguage["tr"] += " drift"; break;
            case "deployable": qa.Deployable = false; break;
            case "error-count": qa.ErrorCount = 1; break;
            case "release-decision": qa.ReleaseDecision.Deployable = false; break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateProductProseCanonicalLinguisticQa(
                qa, prose, productTags, matchesHash),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void ProductProseLinguisticQualityGateAcceptsExactBoundary()
    {
        var gate = CreateProductProseLinguisticQualityGate();

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateProductProseLinguisticQualityGateContract(
                gate, (_, _) => true));
    }

    [TestCase("additional-edit-count")]
    [TestCase("additional-edit-row-count")]
    [TestCase("deployable")]
    [TestCase("route-count")]
    [TestCase("sentence-count")]
    [TestCase("authority-review-count")]
    [TestCase("sentence-review-count")]
    [TestCase("linguistic-error")]
    [TestCase("urdu-review-count")]
    [TestCase("urdu-tag-count")]
    [TestCase("urdu-attribute-count")]
    [TestCase("urdu-residual")]
    [TestCase("qa-hash-format")]
    [TestCase("qa-hash-binding")]
    [TestCase("urdu-hash-format")]
    [TestCase("urdu-hash-binding")]
    public void ProductProseLinguisticQualityGateRejectsEveryBoundary(string mutation)
    {
        var gate = CreateProductProseLinguisticQualityGate();
        Func<string, string, bool> matchesHash = (_, _) => true;
        switch (mutation)
        {
            case "additional-edit-count": gate.ProductProseAdditionalEditCount--; break;
            case "additional-edit-row-count": gate.ProductProseAdditionalEditRowCount--; break;
            case "deployable": gate.FinalProductProseLinguisticIndependentQaDeployable = false; break;
            case "route-count": gate.ProductProseLinguisticRouteCount--; break;
            case "sentence-count": gate.ProductProseLinguisticDistinctSentenceCount--; break;
            case "authority-review-count": gate.ProductProseLinguisticAuthorityReviewCount--; break;
            case "sentence-review-count": gate.ProductProseLinguisticSentenceReviewCount--; break;
            case "linguistic-error": gate.ProductProseLinguisticErrorCount++; break;
            case "urdu-review-count": gate.UrduNockLinguisticReviewedRowCount--; break;
            case "urdu-tag-count": gate.UrduNockLinguisticProductTagReviewedRowCount--; break;
            case "urdu-attribute-count": gate.UrduNockLinguisticProductAttributeReviewedRowCount--; break;
            case "urdu-residual": gate.UrduNockLinguisticResidualCount++; break;
            case "qa-hash-format":
                gate.ProductProseCanonicalLinguisticIndependentQaSha256 = new string('Z', 64);
                break;
            case "qa-hash-binding":
                matchesHash = (file, _) =>
                    file != "product-prose-canonical-linguistic-independent-qa.json";
                break;
            case "urdu-hash-format":
                gate.UrduNockLinguisticCorrectionAuditSha256 = new string('Z', 64);
                break;
            case "urdu-hash-binding":
                matchesHash = (file, _) => file != "urdu-nock-linguistic-correction-audit.json";
                break;
            default: Assert.Fail($"Unknown mutation '{mutation}'."); break;
        }

        Assert.That(() =>
                LocalizationResourceInstaller.ValidateProductProseLinguisticQualityGateContract(
                    gate, matchesHash),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("linguistic quality gate"));
    }

    [Test]
    public void SupplementalProductSlugQualityGateAcceptsExactSchema7Boundary()
    {
        Assert.DoesNotThrow(() => LocalizationResourceInstaller
            .ValidateProductProseSupplementalSlugQualityGateContract(
                CreateSupplementalProductSlugQualityGate()));
    }

    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.SchemaVersion))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalSlugPolicyVersion))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalReviewedSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalSlugSourceNameLinkCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalPreviousSlugRedirectCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalUniqueSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalUniquePreviousSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalProhibitedSlugTermCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalSlugTargetSetSha256))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductProseSupplementalSlugErrorCount))]
    public void SupplementalProductSlugQualityGateRejectsEveryBoundary(string boundary)
    {
        var gate = CreateSupplementalProductSlugQualityGate();
        var property = typeof(LocalizationResourceInstaller.LocalizationQualityGate)
            .GetProperty(boundary) ?? throw new InvalidOperationException(boundary);
        if (property.PropertyType == typeof(string))
            property.SetValue(gate, new string('A', 64));
        else
        {
            var current = (int)(property.GetValue(gate) ?? 0);
            property.SetValue(gate, current == 0 ? 1 : current - 1);
        }

        Assert.That(() => LocalizationResourceInstaller
                .ValidateProductProseSupplementalSlugQualityGateContract(gate),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains(
                "supplemental product-slug quality gate"));
    }

    [Test]
    public void RegularLocalizationAndProductProseTuplesMayNotOverlap()
    {
        var regular = new List<LocalizationResourceInstaller.LocalizedRow>
        {
            new() { EntityId = 17, LanguageId = 1, Group = "Product", Key = "FullDescription" }
        };
        var prose = new List<LocalizationResourceInstaller.ProductProseKocCorrection>
        {
            new()
            {
                TargetKind = "LocalizedProperty", EntityId = 17, LanguageCode = "en",
                Field = "FullDescription"
            }
        };
        var languages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["en"] = 1 };

        Assert.That(() => LocalizationResourceInstaller.ValidateNoProductProseTupleOverlap(
                regular, prose, languages),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("overlap"));

        prose[0].EntityId = 18;
        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateNoProductProseTupleOverlap(
            regular, prose, languages));
    }

    [Test]
    public void FinalSchema7EvidenceContractIsAccepted()
    {
        var gate = CreateFinalQualityGate();

        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(
            gate, (_, _) => true));
    }

    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalUnchangedLabelIndependentQaDeployable))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalLarpSlugLinguisticIndependentQaDeployable))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductLabelCorrectionCheckCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductLabelCorrectedRowCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductTagCorrectedRowCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductAttributeCorrectionGroupCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductAttributeCorrectedRowCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalAcceptedLabelReviewedWarningRowCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductRemainingReviewedWarningRowCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductTagPreviousSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.RetainedPreviousSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.ReviewedLarpSlugMappingCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.NumericLarpCollisionCounterSlugCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.LiveUrlRecordOwnerConflictCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.DisavowedForeignProductTagHistoryCount))]
    [TestCase(nameof(LocalizationResourceInstaller.LocalizationQualityGate.KocNockCheckCount))]
    public void FinalSchema7EvidenceContractRejectsEveryProductLabelAndLarpBoundary(string boundary)
    {
        var gate = CreateFinalQualityGate();
        switch (boundary)
        {
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalUnchangedLabelIndependentQaDeployable):
                gate.FinalUnchangedLabelIndependentQaDeployable = false;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalLarpSlugLinguisticIndependentQaDeployable):
                gate.FinalLarpSlugLinguisticIndependentQaDeployable = false;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductLabelCorrectionCheckCount):
                gate.FinalProductLabelCorrectionCheckCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductLabelCorrectedRowCount):
                gate.FinalProductLabelCorrectedRowCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductTagCorrectedRowCount):
                gate.FinalProductTagCorrectedRowCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductAttributeCorrectionGroupCount):
                gate.FinalProductAttributeCorrectionGroupCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductAttributeCorrectedRowCount):
                gate.FinalProductAttributeCorrectedRowCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalAcceptedLabelReviewedWarningRowCount):
                gate.FinalAcceptedLabelReviewedWarningRowCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.FinalProductRemainingReviewedWarningRowCount):
                gate.FinalProductRemainingReviewedWarningRowCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.ProductTagPreviousSlugCount):
                gate.ProductTagPreviousSlugCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.RetainedPreviousSlugCount):
                gate.RetainedPreviousSlugCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.ReviewedLarpSlugMappingCount):
                gate.ReviewedLarpSlugMappingCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.NumericLarpCollisionCounterSlugCount):
                gate.NumericLarpCollisionCounterSlugCount++;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.LiveUrlRecordOwnerConflictCount):
                gate.LiveUrlRecordOwnerConflictCount++;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.DisavowedForeignProductTagHistoryCount):
                gate.DisavowedForeignProductTagHistoryCount--;
                break;
            case nameof(LocalizationResourceInstaller.LocalizationQualityGate.KocNockCheckCount):
                gate.KocNockCheckCount--;
                break;
            default:
                Assert.Fail($"Unknown boundary: {boundary}");
                break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate, (_, _) => true),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));
    }

    [Test]
    public void FinalSchema7EvidenceContractRejectsWrongThresholdMissingFileAndNonHexHash()
    {
        var gate = CreateFinalQualityGate();
        gate.FinalManifestResolvedTupleCount--;
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate, (_, _) => true),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));

        gate = CreateFinalQualityGate();
        gate.FinalEvidenceReportSha256.Remove("slug-regeneration-audit.json");
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate, (_, _) => true),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));

        gate = CreateFinalQualityGate();
        gate.FinalEvidenceReportSha256["slug-regeneration-audit.json"] = new string('Z', 64);
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate, (_, _) => true),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));

        gate = CreateFinalQualityGate();
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate,
                (file, _) => file != "slug-regeneration-audit.json"),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));

        gate = CreateFinalQualityGate();
        gate.LiveUrlRecordOwnershipIndependentQaSha256 = new string('A', 63);
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate, (_, _) => true),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));

        gate = CreateFinalQualityGate();
        Assert.That(() => LocalizationResourceInstaller.ValidateFinalEvidenceContract(gate,
                (file, _) => file != "live-urlrecord-ownership-independent-qa.json"),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("quality gate"));
    }

    [Test]
    public void DisavowedForeignPreviousSlugEvidenceIsAccepted()
    {
        var (manifest, productTags) = CreateDisavowedEvidencePackage();

        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
            manifest, productTags));
    }

    [Test]
    public void DisavowedForeignPreviousSlugEvidenceRejectsInvalidWitnesses()
    {
        var (manifest, productTags) = CreateDisavowedEvidencePackage();
        manifest.DisavowedForeignPreviousSlugs[0].SameOwnerUrlRecordIds[0] = 0;
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("evidence is invalid"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        manifest.DisavowedForeignPreviousSlugs[0].ForeignOwners[0].IsActive = null;
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("evidence is invalid"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        var evidence = manifest.DisavowedForeignPreviousSlugs[0];
        evidence.ForeignOwners[0].EntityType = evidence.EntityType;
        evidence.ForeignOwners[0].EntityId = evidence.EntityId;
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("evidence is invalid"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        manifest.DisavowedForeignPreviousSlugs[0].ForeignOwners[0].LanguageId++;
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("evidence is invalid"));
    }

    [Test]
    public void DisavowedSlugCannotRemainInAnyCurrentOrPreviousClaim()
    {
        var (manifest, productTags) = CreateDisavowedEvidencePackage();
        var disavowed = manifest.DisavowedForeignPreviousSlugs[0].Slug;
        manifest.Entries.Add(new LocalizationResourceInstaller.ManifestEntry { Slug = disavowed });
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("still claimed"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        disavowed = manifest.DisavowedForeignPreviousSlugs[0].Slug;
        manifest.Entries.Add(new LocalizationResourceInstaller.ManifestEntry
            { Slug = "manifest-current", PreviousSlugs = new List<string> { disavowed } });
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("still claimed"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        disavowed = manifest.DisavowedForeignPreviousSlugs[0].Slug;
        productTags.Slugs.Add(new LocalizationResourceInstaller.ProductTagSlug { Slug = disavowed });
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("still claimed"));

        (manifest, productTags) = CreateDisavowedEvidencePackage();
        disavowed = manifest.DisavowedForeignPreviousSlugs[0].Slug;
        productTags.Slugs.Add(new LocalizationResourceInstaller.ProductTagSlug
            { Slug = "tag-current", PreviousSlugs = new List<string> { disavowed } });
        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugs(
                manifest, productTags),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("still claimed"));
    }

    [Test]
    public void DisavowedForeignPreviousSlugDatabaseWitnessesAreAccepted()
    {
        var (evidenceRows, currentRecords) = CreateDisavowedDatabaseWitnesses();

        Assert.DoesNotThrow(() =>
            LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugRecords(
                evidenceRows, currentRecords));
    }

    [TestCase("missing-same-owner")]
    [TestCase("same-owner-entity-name")]
    [TestCase("same-owner-entity-id")]
    [TestCase("same-owner-language-id")]
    [TestCase("same-owner-slug")]
    [TestCase("foreign-entity-name")]
    [TestCase("foreign-entity-id")]
    [TestCase("foreign-language-id")]
    [TestCase("foreign-slug")]
    [TestCase("foreign-is-active")]
    public void DisavowedForeignPreviousSlugDatabaseWitnessDriftIsRejected(string mutation)
    {
        var (evidenceRows, currentRecords) = CreateDisavowedDatabaseWitnesses();
        var sameOwner = currentRecords.Single(record => record.Id == 1001);
        var foreignOwner = currentRecords.Single(record => record.Id == 2001);
        switch (mutation)
        {
            case "missing-same-owner":
                currentRecords.Remove(sameOwner);
                break;
            case "same-owner-entity-name":
                sameOwner.EntityName = "product";
                break;
            case "same-owner-entity-id":
                sameOwner.EntityId++;
                break;
            case "same-owner-language-id":
                sameOwner.LanguageId++;
                break;
            case "same-owner-slug":
                sameOwner.Slug += "-drift";
                break;
            case "foreign-entity-name":
                foreignOwner.EntityName = "product";
                break;
            case "foreign-entity-id":
                foreignOwner.EntityId++;
                break;
            case "foreign-language-id":
                foreignOwner.LanguageId++;
                break;
            case "foreign-slug":
                foreignOwner.Slug += "-drift";
                break;
            case "foreign-is-active":
                foreignOwner.IsActive = !foreignOwner.IsActive;
                break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateDisavowedForeignPreviousSlugRecords(
                evidenceRows, currentRecords),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("no longer match"));
    }

    [Test]
    public void ExactReviewedRetiredUrlPackageContractIsAccepted()
    {
        Assert.DoesNotThrow(() => LocalizationResourceInstaller.ValidateRetiredUrlRecordPackageContract(
            new List<int> { 708 }, new List<LocalizationResourceInstaller.RetiredUrlRecord>
            {
                CreateReviewedRetiredUrlRecord()
            }));
    }

    [TestCase("id-list")]
    [TestCase("id")]
    [TestCase("entity-id")]
    [TestCase("entity-name")]
    [TestCase("slug")]
    [TestCase("language-id")]
    public void ReviewedRetiredUrlPackageContractDriftIsRejected(string mutation)
    {
        var ids = new List<int> { 708 };
        var record = CreateReviewedRetiredUrlRecord();
        switch (mutation)
        {
            case "id-list": ids[0]++; break;
            case "id": record.Id++; break;
            case "entity-id": record.EntityId++; break;
            case "entity-name": record.EntityName = "producttag"; break;
            case "slug": record.Slug += "-drift"; break;
            case "language-id": record.LanguageId++; break;
        }

        Assert.That(() => LocalizationResourceInstaller.ValidateRetiredUrlRecordPackageContract(
                ids, new List<LocalizationResourceInstaller.RetiredUrlRecord> { record }),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("package contract"));
    }

    private static LocalizationResourceInstaller.LocalizationQualityGate CreateFinalQualityGate()
    {
        var evidenceFiles = new[]
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
        return new LocalizationResourceInstaller.LocalizationQualityGate
        {
            SchemaVersion = 7,
            FinalManifestIndependentQaDeployable = true,
            FinalProductIndependentQaDeployable = true,
            FinalUnchangedLabelIndependentQaDeployable = true,
            FinalLarpSlugLinguisticIndependentQaDeployable = true,
            FinalManifestResolvedTupleCount = 357,
            PublicMetaDescriptionCompletenessCheckCount = 479,
            FinalProductCorrectionCheckCount = 43,
            FinalProductLabelCorrectionCheckCount = 203,
            FinalProductLabelCorrectedRowCount = 635,
            FinalProductTagCorrectedRowCount = 119,
            FinalProductAttributeCorrectionGroupCount = 84,
            FinalProductAttributeCorrectedRowCount = 516,
            FinalAcceptedLabelReviewedWarningRowCount = 6804,
            FinalProductRemainingReviewedWarningRowCount = 6778,
            KocNockCheckCount = 1224,
            OpaqueTypeCodeCheckCount = 4248,
            ManifestPreviousSlugCount = 107,
            ProductTagPreviousSlugCount = 7048,
            RetainedPreviousSlugCount = 7155,
            DisavowedForeignPreviousSlugCount = 20,
            ReviewedLarpSlugMappingCount = 63,
            NumericLarpCollisionCounterSlugCount = 0,
            LiveUrlRecordOwnerConflictCount = 0,
            DisavowedForeignProductTagHistoryCount = 1,
            LiveUrlRecordOwnershipIndependentQaSha256 = new string('A', 64),
            FinalEvidenceReportSha256 = evidenceFiles.ToDictionary(file => file, _ => new string('A', 64),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static LocalizationResourceInstaller.LocalizationQualityGate
        CreateProductProseLinguisticQualityGate() => new()
    {
        SchemaVersion = 7,
        ProductProseAdditionalEditCount = 3,
        ProductProseAdditionalEditRowCount = 2,
        FinalProductProseLinguisticIndependentQaDeployable = true,
        ProductProseLinguisticRouteCount = 24,
        ProductProseLinguisticDistinctSentenceCount = 20,
        ProductProseLinguisticAuthorityReviewCount = 20,
        ProductProseLinguisticSentenceReviewCount = 20,
        ProductProseLinguisticErrorCount = 0,
        UrduNockLinguisticReviewedRowCount = 254,
        UrduNockLinguisticProductTagReviewedRowCount = 4,
        UrduNockLinguisticProductAttributeReviewedRowCount = 250,
        UrduNockLinguisticResidualCount = 0,
        ProductProseCanonicalLinguisticIndependentQaSha256 = new string('A', 64),
        UrduNockLinguisticCorrectionAuditSha256 = new string('B', 64)
    };

    private static LocalizationResourceInstaller.LocalizationQualityGate
        CreateSupplementalProductSlugQualityGate() => new()
    {
        SchemaVersion = 7,
        ProductProseSupplementalSlugPolicyVersion = 2,
        ProductProseSupplementalReviewedSlugCount = 8,
        ProductProseSupplementalSlugSourceNameLinkCount = 8,
        ProductProseSupplementalPreviousSlugRedirectCount = 8,
        ProductProseSupplementalUniqueSlugCount = 8,
        ProductProseSupplementalUniquePreviousSlugCount = 8,
        ProductProseSupplementalProhibitedSlugTermCount = 0,
        ProductProseSupplementalSlugTargetSetSha256 =
            "70CE09B77B5AB4E42DBFBBE4E1C7494CC5C778665AA89DE748339E30422FEAC2",
        ProductProseSupplementalSlugErrorCount = 0
    };

    private static (LocalizationResourceInstaller.LocalizationManifest Manifest,
        LocalizationResourceInstaller.ProductTagPackage ProductTags) CreateDisavowedEvidencePackage()
    {
        var manifest = new LocalizationResourceInstaller.LocalizationManifest
        {
            DisavowedForeignPreviousSlugCount = 20,
            DisavowedForeignPreviousSlugs = Enumerable.Range(1, 20).Select(index =>
                new LocalizationResourceInstaller.DisavowedForeignPreviousSlug
                {
                    EntityType = "Product",
                    EntityId = 340 + index,
                    LanguageId = index,
                    LanguageCode = $"x{index}",
                    Slug = $"disavowed-{index}",
                    Reason = "foreign-owner-preserved",
                    SameOwnerUrlRecordIds = new List<int> { 1000 + index },
                    ForeignOwners = new List<LocalizationResourceInstaller.ForeignSlugOwner>
                    {
                        new()
                        {
                            UrlRecordId = 2000 + index,
                            EntityType = "Product",
                            EntityId = 500 + index,
                            LanguageId = index,
                            IsActive = index % 2 == 0
                        }
                    }
                }).ToList()
        };
        return (manifest, new LocalizationResourceInstaller.ProductTagPackage());
    }

    private static (List<LocalizationResourceInstaller.DisavowedForeignPreviousSlug> EvidenceRows,
        List<UrlRecord> CurrentRecords) CreateDisavowedDatabaseWitnesses()
    {
        var (manifest, _) = CreateDisavowedEvidencePackage();
        var evidence = manifest.DisavowedForeignPreviousSlugs[0];
        return (new List<LocalizationResourceInstaller.DisavowedForeignPreviousSlug> { evidence },
            new List<UrlRecord>
            {
                new()
                {
                    Id = evidence.SameOwnerUrlRecordIds[0],
                    EntityName = evidence.EntityType,
                    EntityId = evidence.EntityId,
                    LanguageId = evidence.LanguageId,
                    Slug = evidence.Slug,
                    IsActive = false
                },
                new()
                {
                    Id = evidence.ForeignOwners[0].UrlRecordId,
                    EntityName = evidence.ForeignOwners[0].EntityType,
                    EntityId = evidence.ForeignOwners[0].EntityId,
                    LanguageId = evidence.ForeignOwners[0].LanguageId,
                    Slug = evidence.Slug,
                    IsActive = evidence.ForeignOwners[0].IsActive!.Value
                }
            });
    }

    private static LocalizationResourceInstaller.RetiredUrlRecord CreateReviewedRetiredUrlRecord() => new()
    {
        Id = 708,
        EntityId = 443,
        EntityName = "ProductTag",
        Slug = "kleidung-mittelalter",
        LanguageId = 0
    };

    private static string SourceHash(params string[] values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\0', values.Select(value => value ?? string.Empty)))));

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static LocalizationResourceInstaller.ProductProseKocCorrection
        CreateAdditionalEditCorrection()
    {
        const string oldSentence = "Tell me whether you prefer ram nock or standard nock.";
        const string canonical = "Please tell me whether you prefer a Koç Nock or a standard nock.";
        var oldValue = $"<p>{oldSentence} Batur ناک and ہارن/بون ناک.</p>";
        var newValue = $"<p>{canonical} Batur Nock and ہارن/بون Nock.</p>";
        return new LocalizationResourceInstaller.ProductProseKocCorrection
        {
            TargetKind = "LocalizedProperty",
            EntityId = 169,
            Sku = "arrow34",
            LanguageCode = "ur",
            Field = "FullDescription",
            OldValue = oldValue,
            NewValue = newValue,
            OldSentence = oldSentence,
            ReplacementSpan = canonical,
            CanonicalSentence = canonical,
            OldSha256 = Sha256(oldValue),
            NewSha256 = Sha256(newValue),
            AdditionalEdits = new List<LocalizationResourceInstaller.ProductProseAdditionalEdit>
            {
                new()
                {
                    OldText = "Batur ناک", NewText = "Batur Nock",
                    Reason = "Reviewed Urdu component correction."
                },
                new()
                {
                    OldText = "ہارن/بون ناک", NewText = "ہارن/بون Nock",
                    Reason = "Reviewed Urdu component correction."
                }
            }
        };
    }

    private static LocalizationResourceInstaller.ProductProseKocCorrection CreateProductProseCorrection(
        string oldValue, string newValue) => new()
    {
        TargetKind = "LocalizedProperty",
        EntityId = 17,
        Sku = "arrow3",
        LanguageCode = "en",
        Field = "FullDescription",
        OldValue = oldValue,
        NewValue = newValue,
        OldSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(oldValue))),
        NewSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newValue)))
    };

    private static LocalizationResourceInstaller.ProductProseSupplementalCorrection
        CreateSupplementalCorrection(string oldValue, string newValue) => new()
    {
        EntityId = 17,
        Sku = "arrow3",
        LanguageCode = "ur",
        Field = "FullDescription",
        OldValue = oldValue,
        NewValue = newValue,
        OldSha256 = Sha256(oldValue),
        NewSha256 = Sha256(newValue),
        TransformationKind = "exactEdits",
        Edits = new List<LocalizationResourceInstaller.ProductProseAdditionalEdit>
        {
            new() { OldText = oldValue, NewText = newValue, Reason = "Reviewed exact change." }
        }
    };
}
