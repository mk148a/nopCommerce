using Moq;
using System.Text.Json;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Payments.StripeAffirm;
using Nop.Plugin.Payments.StripeAfterpay;
using Nop.Plugin.Payments.StripeKlarna;
using Nop.Plugin.Payments.StripeZip;
using Nop.Services.Localization;
using Nop.Services.Payments;
using NUnit.Framework;

namespace Nop.Tests.Nop.Plugin.Misc.StripeBnplCore.Tests;

[TestFixture]
public class ProviderPaymentMethodContractTests
{
    private static readonly IReadOnlyDictionary<BnplProvider, string> Descriptions =
        new Dictionary<BnplProvider, string>
        {
            [BnplProvider.Klarna] = "Pay now or pay over time with Klarna. Available options depend on your location, order amount, and Klarna approval.",
            [BnplProvider.Affirm] = "Pay in 4 or choose monthly payments, if eligible. Affirm shows available rates and terms before you agree.",
            [BnplProvider.Afterpay] = "Split your purchase into installments, if eligible. Available plans and terms depend on your country and provider approval.",
            [BnplProvider.Zip] = "Pay in 4 with Zip, if eligible. Zip shows any customer fee and all terms before you agree."
        };

    [TestCase(BnplProvider.Klarna)]
    [TestCase(BnplProvider.Affirm)]
    [TestCase(BnplProvider.Afterpay)]
    [TestCase(BnplProvider.Zip)]
    public async Task ProviderIsSeparateRedirectMethodWithDescriptionAndNoSurcharge(BnplProvider provider)
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService
            .Setup(service => service.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync(Descriptions[provider]);
        var bnplService = new Mock<IStripeBnplService>();
        var paymentMethod = CreatePaymentMethod(provider, localizationService.Object, bnplService.Object);

        var fee = await paymentMethod.GetAdditionalHandlingFeeAsync(new List<ShoppingCartItem>());
        var description = await paymentMethod.GetPaymentMethodDescriptionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(paymentMethod.PaymentMethodType, Is.EqualTo(PaymentMethodType.Redirection));
            Assert.That(paymentMethod.SkipPaymentInfo, Is.True);
            Assert.That(paymentMethod.RecurringPaymentType, Is.EqualTo(RecurringPaymentType.NotSupported));
            Assert.That(paymentMethod.SupportCapture, Is.False);
            Assert.That(paymentMethod.SupportRefund, Is.False);
            Assert.That(paymentMethod.SupportPartiallyRefund, Is.False);
            Assert.That(paymentMethod.SupportVoid, Is.False);
            Assert.That(fee, Is.EqualTo(decimal.Zero));
            Assert.That(description, Is.EqualTo(Descriptions[provider]));
        });
    }

    [TestCase(BnplProvider.Klarna)]
    [TestCase(BnplProvider.Affirm)]
    [TestCase(BnplProvider.Afterpay)]
    [TestCase(BnplProvider.Zip)]
    public async Task ProviderDelegatesFailClosedVisibilityToCore(BnplProvider provider)
    {
        var cart = new List<ShoppingCartItem> { new() { ProductId = 100 } };
        var bnplService = new Mock<IStripeBnplService>();
        bnplService.Setup(service => service.ShouldHidePaymentMethodAsync(provider, cart)).ReturnsAsync(true);
        var paymentMethod = CreatePaymentMethod(provider, Mock.Of<ILocalizationService>(), bnplService.Object);

        var hidden = await paymentMethod.HidePaymentMethodAsync(cart);

        Assert.That(hidden, Is.True);
        bnplService.Verify(service => service.ShouldHidePaymentMethodAsync(provider, cart), Times.Once);
    }

    [Test]
    public void ExistingStripeCardAndWalletSystemNamesRemainUnchanged()
    {
        Assert.Multiple(() =>
        {
            Assert.That(global::Nop.Plugin.Payments.Stripe.StripePaymentDefaults.SystemName, Is.EqualTo("Payments.Stripe"));
            Assert.That(global::Nop.Plugin.Payments.StripeApplePay.StripeApplePayPaymentDefaults.SystemName,
                Is.EqualTo("Payments.StripeApplePay"));
        });
    }

    [TestCase("Klarna", "Payments.StripeKlarna")]
    [TestCase("Affirm", "Payments.StripeAffirm")]
    [TestCase("Afterpay", "Payments.StripeAfterpay")]
    [TestCase("Zip", "Payments.StripeZip")]
    public void ProviderManifestDefinesSeparatePaymentOptionAndLogo(string providerName, string systemName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginDirectory = Path.Combine(repositoryRoot, "src", "Plugins",
            $"Nop.Plugin.Payments.Stripe{providerName}");
        var manifestPath = Path.Combine(pluginDirectory, "plugin.json");

        Assert.That(File.Exists(manifestPath), Is.True, $"Missing provider manifest: {manifestPath}");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var dependencies = root.GetProperty("DependsOnSystemNames")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("Group").GetString(), Is.EqualTo("Payment methods"));
            Assert.That(root.GetProperty("SystemName").GetString(), Is.EqualTo(systemName));
            Assert.That(root.GetProperty("FriendlyName").GetString(), Does.StartWith(providerName));
            Assert.That(dependencies, Does.Contain("Misc.StripeBnplCore"));
            Assert.That(File.Exists(Path.Combine(pluginDirectory, "logo.png")), Is.True);
        });
    }

    private static IPaymentMethod CreatePaymentMethod(
        BnplProvider provider,
        ILocalizationService localizationService,
        IStripeBnplService stripeBnplService)
    {
        return provider switch
        {
            BnplProvider.Klarna => new StripeKlarnaPaymentMethod(localizationService, stripeBnplService),
            BnplProvider.Affirm => new StripeAffirmPaymentMethod(localizationService, stripeBnplService),
            BnplProvider.Afterpay => new StripeAfterpayPaymentMethod(localizationService, stripeBnplService),
            BnplProvider.Zip => new StripeZipPaymentMethod(localizationService, stripeBnplService),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
