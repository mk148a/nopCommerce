using Moq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class BnplEligibilityFailClosedTests
{
    [Test]
    public async Task UnhealthyPaymentMethodConfigurationHidesOtherwiseEligibleMethod()
    {
        var settings = CreateConfiguredTestSettings();
        var eligibilityService = new Mock<IBnplEligibilityService>();
        eligibilityService
            .Setup(service => service.EvaluateCartAsync(It.IsAny<BnplProvider>(), It.IsAny<IList<ShoppingCartItem>>()))
            .ReturnsAsync(BnplCartEligibilityResult.Eligible());
        var configurationClient = new Mock<IStripeBnplConfigurationClient>();
        configurationClient
            .Setup(client => client.CheckProviderOnlyAsync(
                It.IsAny<BnplProvider>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new StripeBnplConfigurationHealth(false, "configuration_unhealthy"));
        var service = new StripeBnplService(
            eligibilityService.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IStripeBnplCheckoutService>(),
            configurationClient.Object,
            settings,
            Mock.Of<IWebHelper>());

        var hidden = await service.ShouldHidePaymentMethodAsync(
            BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(hidden, Is.True);
        configurationClient.Verify(client => client.CheckProviderOnlyAsync(
            BnplProvider.Klarna, "pmc_test_klarna", false), Times.Once);
    }

    [Test]
    public async Task EmptyCartIsNotEligible()
    {
        var service = CreateService(CreateConfiguredTestSettings(), safeEnvironment: true);

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna, new List<ShoppingCartItem>());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("cart_empty"));
            Assert.That(result.BlockingProductIds, Is.Empty);
        });
    }

    [Test]
    public async Task UnsafeSandboxDatabaseBlocksEveryCartProduct()
    {
        var service = CreateService(CreateConfiguredTestSettings(), safeEnvironment: false);
        var cart = new List<ShoppingCartItem>
        {
            new() { ProductId = 10 },
            new() { ProductId = 20 },
            new() { ProductId = 10 }
        };

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna, cart);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("environment_unsafe"));
            Assert.That(result.BlockingProductIds, Is.EquivalentTo(new[] { 10, 20 }));
        });
    }

    [Test]
    public async Task MissingRestrictedKeyBlocksPaymentMethod()
    {
        var settings = CreateConfiguredTestSettings();
        settings.TestRestrictedKey = null;
        var service = CreateService(settings, safeEnvironment: true);

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("api_key_missing"));
    }

    [Test]
    public async Task MissingProviderConfigurationBlocksPaymentMethod()
    {
        var settings = CreateConfiguredTestSettings();
        settings.TestAffirmConfigurationId = null;
        var service = CreateService(settings, safeEnvironment: true);

        var result = await service.EvaluateCartAsync(BnplProvider.Affirm,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("payment_configuration_missing"));
    }

    [Test]
    public async Task LiveModeWithoutWrittenProviderApprovalIsHidden()
    {
        var settings = CreateConfiguredTestSettings();
        settings.UseSandbox = false;
        settings.LiveRestrictedKey = "rk_live_placeholder";
        settings.LiveAfterpayConfigurationId = "pmc_live_afterpay";
        settings.AfterpayApprovalReference = null;
        var service = CreateService(settings, safeEnvironment: true);

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("live_approval_missing"));
        });
    }

    [Test]
    public async Task LegacyToggleCannotBypassLiveWrittenProviderApproval()
    {
        var settings = CreateConfiguredTestSettings();
        settings.UseSandbox = false;
#pragma warning disable CS0618
        settings.RequireWrittenApprovalForLive = false;
#pragma warning restore CS0618
        settings.LiveRestrictedKey = "rk_live_placeholder";
        settings.LiveKlarnaConfigurationId = "pmc_live_klarna";
        settings.KlarnaApprovalReference = null;
        var service = CreateService(settings, safeEnvironment: true);

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("live_approval_missing"));
    }

    [Test]
    public async Task UnknownProviderIsRejectedBeforeAnyExternalWork()
    {
        var service = CreateService(CreateConfiguredTestSettings(), safeEnvironment: true);

        var result = await service.EvaluateCartAsync((BnplProvider)999,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("provider_invalid"));
    }

    [Test]
    public async Task MixedCartIsBlockedWhenAnyProductIsUnknown()
    {
        var records = new[]
        {
            AllowedRecord(10, BnplProvider.Klarna)
        };
        var service = CreateService(CreateConfiguredTestSettings(), true, records);
        var cart = new List<ShoppingCartItem>
        {
            new() { ProductId = 10 },
            new() { ProductId = 20 }
        };

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna, cart);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("product_unknown"));
            Assert.That(result.BlockingProductIds, Is.EquivalentTo(new[] { 20 }));
        });
    }

    [TestCase(BnplEligibilityState.Unknown, "product_unknown")]
    [TestCase(BnplEligibilityState.ApprovalRequired, "product_approvalrequired")]
    [TestCase(BnplEligibilityState.Prohibited, "product_prohibited")]
    public async Task AnyStateOtherThanAllowedBlocksTheProduct(BnplEligibilityState state, string reasonCode)
    {
        var record = AllowedRecord(10, BnplProvider.Affirm);
        record.EligibilityState = state;
        var service = CreateService(CreateConfiguredTestSettings(), true, new[] { record });

        var result = await service.EvaluateCartAsync(BnplProvider.Affirm,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo(reasonCode));
            Assert.That(result.BlockingProductIds, Is.EquivalentTo(new[] { 10 }));
        });
    }

    [Test]
    public async Task RestrictedCatalogTextNeedsWrittenProductException()
    {
        var service = CreateService(
            CreateConfiguredTestSettings(),
            true,
            new[] { AllowedRecord(10, BnplProvider.Klarna) },
            new Dictionary<int, string> { [10] = "Traditional archery bow" });

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsEligible, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("restricted_catalog_term"));
            Assert.That(result.Reason, Does.Contain("archery"));
        });
    }

    [Test]
    public async Task WrittenProductExceptionAllowsRestrictedCatalogText()
    {
        var record = AllowedRecord(10, BnplProvider.Klarna);
        record.ApprovalReference = "STRIPE-CASE-123";
        var service = CreateService(
            CreateConfiguredTestSettings(),
            true,
            new[] { record },
            new Dictionary<int, string> { [10] = "Traditional archery bow" });

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.IsEligible, Is.True);
    }

    [TestCase(null)]
    [TestCase(28)]
    public async Task AfterpayAllowsUnknownOrLongFulfillmentWhenNoOptionalLimitConfigured(int? fulfillmentDays)
    {
        var record = AllowedRecord(10, BnplProvider.Afterpay);
        record.FulfillmentDays = fulfillmentDays;
        var service = CreateService(CreateConfiguredTestSettings(), true, new[] { record });

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.IsEligible, Is.True);
    }

    [Test]
    public async Task AfterpayOptionalMaximumBlocksLongFulfillmentWhenConfigured()
    {
        var settings = CreateConfiguredTestSettings();
        settings.AfterpayMaximumFulfillmentDays = 14;
        var service = CreateService(settings, true,
            new[] { AllowedRecord(10, BnplProvider.Afterpay, fulfillmentDays: 15) });

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("afterpay_fulfillment_window"));
    }

    [Test]
    public async Task AfterpayRejectsNegativeFulfillmentDays()
    {
        var record = AllowedRecord(10, BnplProvider.Afterpay);
        record.FulfillmentDays = -1;
        var service = CreateService(CreateConfiguredTestSettings(), true, new[] { record });

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("afterpay_fulfillment_invalid"));
    }

    [Test]
    public async Task AfterpayRejectsCorruptNegativeOptionalMaximum()
    {
        var settings = CreateConfiguredTestSettings();
        settings.AfterpayMaximumFulfillmentDays = -1;
        var service = CreateService(settings, true,
            new[] { AllowedRecord(10, BnplProvider.Afterpay, fulfillmentDays: 28) });

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.ReasonCode, Is.EqualTo("afterpay_fulfillment_configuration_invalid"));
    }

    [Test]
    public async Task SnapshotCaptureRechecksConfiguredAfterpayMaximumAgainstSecondRead()
    {
        var settings = CreateConfiguredTestSettings();
        settings.AfterpayMaximumFulfillmentDays = 7;
        var firstRead = AllowedRecord(10, BnplProvider.Afterpay, fulfillmentDays: 2);
        var secondRead = AllowedRecord(10, BnplProvider.Afterpay, fulfillmentDays: 8);
        var (service, store) = CreateServiceWithStore(settings, true, new[] { firstRead });
        store.SetupSequence(item => item.GetByProductIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), BnplProvider.Afterpay))
            .ReturnsAsync(new[] { firstRead })
            .ReturnsAsync(new[] { secondRead });

        var order = new Order { Id = 501, OrderGuid = Guid.NewGuid(), CustomerId = 12, StoreId = 1 };
        var orderItems = new List<OrderItem> { new() { Id = 601, OrderId = order.Id, ProductId = 10, Quantity = 1 } };

        var exception = Assert.ThrowsAsync<NopException>(async () => await service.CaptureOrderSnapshotAsync(
            BnplProvider.Afterpay, order, orderItems, 10_000, "usd"));

        Assert.That(exception.Message, Does.Contain("afterpay_fulfillment_window"));
    }

    [Test]
    public async Task FullyApprovedMixedCartIsEligible()
    {
        var records = new[]
        {
            AllowedRecord(10, BnplProvider.Afterpay, fulfillmentDays: 14),
            AllowedRecord(20, BnplProvider.Afterpay, fulfillmentDays: 2)
        };
        var (service, store) = CreateServiceWithStore(CreateConfiguredTestSettings(), true, records);
        var cart = new List<ShoppingCartItem>
        {
            new() { ProductId = 10 },
            new() { ProductId = 20 },
            new() { ProductId = 10 }
        };

        var result = await service.EvaluateCartAsync(BnplProvider.Afterpay, cart);

        Assert.That(result.IsEligible, Is.True);
        store.Verify(candidate => candidate.GetByProductIdsAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(10) && ids.Contains(20)),
            BnplProvider.Afterpay), Times.Once);
    }

    [TestCase("CA", "USD", 100)]
    [TestCase("US", "EUR", 100)]
    [TestCase("US", "USD", 0.50)]
    public async Task StripeOwnsCountryCurrencyAndAmountEligibility(
        string country,
        string currency,
        decimal amount)
    {
        var cartContext = new BnplCartContext(country, currency, amount);
        var service = CreateService(
            CreateConfiguredTestSettings(),
            true,
            new[] { AllowedRecord(10, BnplProvider.Klarna) },
            cartContext: cartContext);

        var result = await service.EvaluateCartAsync(BnplProvider.Klarna,
            new List<ShoppingCartItem> { new() { ProductId = 10 } });

        Assert.That(result.IsEligible, Is.True);
    }

    private static StripeBnplSettings CreateConfiguredTestSettings()
    {
        return new StripeBnplSettings
        {
            UseSandbox = true,
            TestRestrictedKey = "rk_test_placeholder",
            TestKlarnaConfigurationId = "pmc_test_klarna",
            TestAffirmConfigurationId = "pmc_test_affirm",
            TestAfterpayConfigurationId = "pmc_test_afterpay",
            TestZipConfigurationId = "pmc_test_zip"
        };
    }

    private static BnplProductEligibility AllowedRecord(
        int productId,
        BnplProvider provider,
        int? fulfillmentDays = 1)
    {
        return new BnplProductEligibility
        {
            ProductId = productId,
            Provider = provider,
            EligibilityState = BnplEligibilityState.Allowed,
            FulfillmentDays = fulfillmentDays,
            UpdatedOnUtc = DateTime.UtcNow
        };
    }

    private static BnplEligibilityService CreateService(
        StripeBnplSettings settings,
        bool safeEnvironment,
        IReadOnlyCollection<BnplProductEligibility> records = null,
        IReadOnlyDictionary<int, string> productNames = null,
        BnplCartContext cartContext = null)
    {
        return CreateServiceWithStore(settings, safeEnvironment, records, productNames, cartContext).Service;
    }

    private static (BnplEligibilityService Service, Mock<IBnplProductEligibilityStore> Store) CreateServiceWithStore(
        StripeBnplSettings settings,
        bool safeEnvironment,
        IReadOnlyCollection<BnplProductEligibility> records = null,
        IReadOnlyDictionary<int, string> productNames = null,
        BnplCartContext cartContext = null)
    {
        var environmentGuard = new Mock<IStripeBnplEnvironmentGuard>();
        environmentGuard.Setup(guard => guard.Check()).Returns(new BnplEnvironmentGuardResult(
            safeEnvironment,
            safeEnvironment ? settings.SandboxDatabaseName : "LiveDatabase",
            safeEnvironment ? "Sandbox database identity verified." : "Sandbox database mismatch."));

        var eligibilityStore = new Mock<IBnplProductEligibilityStore>();
        eligibilityStore
            .Setup(store => store.GetByProductIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<BnplProvider>()))
            .ReturnsAsync(records ?? Array.Empty<BnplProductEligibility>());

        var catalogRiskService = new Mock<IBnplCatalogRiskService>();
        catalogRiskService
            .Setup(service => service.FindRestrictedTermAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((int productId, string _) =>
                productNames != null && productNames.TryGetValue(productId, out var name) &&
                name.Contains("archery", StringComparison.OrdinalIgnoreCase)
                    ? "archery"
                    : null);

        var cartContextService = new Mock<IBnplCartContextService>();
        cartContextService
            .Setup(service => service.GetAsync(It.IsAny<IList<ShoppingCartItem>>()))
            .ReturnsAsync(cartContext ?? new BnplCartContext("US", "USD", 100m));
        cartContextService
            .Setup(service => service.GetAsync(It.IsAny<Order>()))
            .ReturnsAsync(cartContext ?? new BnplCartContext("US", "USD", 100m));

        var service = new BnplEligibilityService(
            catalogRiskService.Object,
            cartContextService.Object,
            eligibilityStore.Object,
            environmentGuard.Object,
            settings);

        return (service, eligibilityStore);
    }
}
