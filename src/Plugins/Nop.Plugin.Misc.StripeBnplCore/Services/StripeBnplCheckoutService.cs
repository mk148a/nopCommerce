using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using System.Security.Cryptography;
using System.Text;
using Stripe;
using Stripe.Checkout;
using NopAddress = Nop.Core.Domain.Common.Address;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplCheckoutService : IStripeBnplCheckoutService
{
    private readonly IAddressService _addressService;
    private readonly IBnplEligibilityService _eligibilityService;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IStripeBnplCheckoutSessionStore _sessionStore;
    private readonly IStripeBnplSessionClient _sessionClient;
    private readonly IStripeBnplConfigurationClient _configurationClient;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IStripeBnplEnvironmentGuard _environmentGuard;
    private readonly ILogger _logger;
    private readonly IWebHelper _webHelper;
    private readonly StripeBnplSettings _settings;

    public StripeBnplCheckoutService(
        IAddressService addressService,
        IBnplEligibilityService eligibilityService,
        ICountryService countryService,
        ICurrencyService currencyService,
        IOrderService orderService,
        IProductService productService,
        IStripeBnplCheckoutSessionStore sessionStore,
        IStripeBnplSessionClient sessionClient,
        IStripeBnplConfigurationClient configurationClient,
        IStateProvinceService stateProvinceService,
        IStripeBnplEnvironmentGuard environmentGuard,
        ILogger logger,
        IWebHelper webHelper,
        StripeBnplSettings settings)
    {
        _addressService = addressService;
        _eligibilityService = eligibilityService;
        _countryService = countryService;
        _currencyService = currencyService;
        _orderService = orderService;
        _productService = productService;
        _sessionStore = sessionStore;
        _sessionClient = sessionClient;
        _configurationClient = configurationClient;
        _stateProvinceService = stateProvinceService;
        _environmentGuard = environmentGuard;
        _logger = logger;
        _webHelper = webHelper;
        _settings = settings;
    }

    public async Task<StripeBnplCheckoutResult> CreateAsync(BnplProvider provider, Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _environmentGuard.EnsureSafe();

        if (order.Deleted || order.PaymentStatus != Nop.Core.Domain.Payments.PaymentStatus.Pending)
            throw new NopException("Only a non-deleted pending order can start Stripe BNPL Checkout.");

        if (!string.Equals(order.PaymentMethodSystemName, StripeBnplDefaults.GetSystemName(provider),
                StringComparison.OrdinalIgnoreCase))
            throw new NopException("The order payment method does not match the requested BNPL provider.");

        var orderItems = await _orderService.GetOrderItemsAsync(order.Id);
        var eligibility = await _eligibilityService.EvaluateOrderAsync(provider, order, orderItems);
        if (!eligibility.IsEligible)
            throw new NopException($"Stripe BNPL checkout is not eligible ({eligibility.ReasonCode}): {eligibility.Reason}");

        var apiKey = _settings.GetActiveRestrictedKey();
        var configurationId = _settings.GetPaymentMethodConfigurationId(provider);
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(configurationId))
            throw new NopException("Stripe BNPL credentials or Payment Method Configuration are missing.");
        await _configurationClient.EnsureProviderOnlyAsync(provider, configurationId, expectedLivemode: !_settings.UseSandbox);

        var currency = (order.CustomerCurrencyCode ?? string.Empty).Trim().ToLowerInvariant();
        if (currency.Length != 3)
            throw new NopException("Order currency is invalid.");

        var customerTotal = _currencyService.ConvertCurrency(order.OrderTotal, order.CurrencyRate);
        var amountMinor = ToMinorUnits(customerTotal, currency);
        if (amountMinor <= 0)
            throw new NopException("Stripe BNPL checkout requires a positive order total.");

        var eligibilitySnapshot = await _eligibilityService.CaptureOrderSnapshotAsync(
            provider, order, orderItems, amountMinor, currency);

        var latest = await _sessionStore.GetLatestAsync(order.Id, provider);
        if (latest != null && latest.IsSandbox == _settings.UseSandbox &&
            latest.AmountMinor == amountMinor &&
            string.Equals(latest.Currency, currency, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(latest.EligibilitySnapshotHash, eligibilitySnapshot.Sha256,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(latest.Status, "open", StringComparison.OrdinalIgnoreCase) &&
            latest.ExpiresOnUtc > DateTime.UtcNow.AddMinutes(1))
        {
            var reusable = await _sessionClient.GetAsync(latest.SessionId);
            if (reusable != null && string.Equals(reusable.Status, "open", StringComparison.OrdinalIgnoreCase) &&
                reusable.ExpiresAt > DateTime.UtcNow.AddMinutes(1) && reusable.AmountTotal == amountMinor &&
                string.Equals(reusable.Currency, currency, StringComparison.OrdinalIgnoreCase) &&
                reusable.Livemode == !_settings.UseSandbox &&
                MetadataMatches(reusable.Metadata, order, provider, eligibilitySnapshot.Sha256))
            {
                EnsureTrustedCheckoutUrl(reusable.Url);
                return ToResult(reusable);
            }

            await _sessionStore.UpdateStatusAsync(latest,
                reusable?.Status ?? (latest.ExpiresOnUtc <= DateTime.UtcNow ? "expired" : "not_reusable"),
                reusable?.PaymentIntentId);
        }

        var attempt = await _sessionStore.ReserveAttemptAsync(order.Id, order.OrderGuid, provider, amountMinor, currency,
            _settings.UseSandbox, eligibilitySnapshot);

        var metadata = new Dictionary<string, string>
        {
            ["order_id"] = order.Id.ToString(),
            ["order_guid"] = order.OrderGuid.ToString("D"),
            ["order_number"] = order.CustomOrderNumber ?? order.Id.ToString(),
            ["provider"] = StripeBnplDefaults.GetProviderSlug(provider),
            ["payment_system_name"] = StripeBnplDefaults.GetSystemName(provider),
            ["environment"] = _settings.UseSandbox ? "test" : "live",
            ["eligibility_snapshot_hash"] = eligibilitySnapshot.Sha256
        };

        var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
        var shippingAddress = order.ShippingAddressId.HasValue
            ? await _addressService.GetAddressByIdAsync(order.ShippingAddressId.Value)
            : null;
        var storeLocation = _webHelper.GetStoreLocation().TrimEnd('/');
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodConfiguration = configurationId.Trim(),
            ClientReferenceId = order.OrderGuid.ToString("D"),
            CustomerEmail = billingAddress?.Email,
            BillingAddressCollection = "required",
            SuccessUrl = $"{storeLocation}/{StripeBnplDefaults.ReturnPath}?session_id={{CHECKOUT_SESSION_ID}}&order_guid={order.OrderGuid:D}",
            CancelUrl = $"{storeLocation}/{StripeBnplDefaults.CancelPath}?order_guid={order.OrderGuid:D}",
            ExpiresAt = attempt.ExpiresOnUtc,
            IntegrationIdentifier = BuildIntegrationIdentifier(provider, order.OrderGuid),
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Description = $"nopCommerce order {order.CustomOrderNumber ?? order.Id.ToString()}",
                ReceiptEmail = billingAddress?.Email,
                Metadata = new Dictionary<string, string>(metadata),
                Shipping = await ToStripeShippingAsync(shippingAddress)
            },
            LineItems = await BuildLineItemsAsync(order, orderItems, amountMinor, currency)
        };

        // PaymentMethodTypes is deliberately omitted. The dedicated Payment Method
        // Configuration controls the single BNPL provider shown for this session.
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = attempt.IdempotencyKey
        };
        var session = await _sessionClient.CreateAsync(sessionOptions, requestOptions);

        await _sessionStore.CompleteAttemptAsync(attempt, session);

        if (session.AmountTotal != amountMinor ||
            !string.Equals(session.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe Checkout total or currency does not match the nopCommerce order.");
        EnsureTrustedCheckoutUrl(session.Url);

        if (!string.IsNullOrWhiteSpace(session.PaymentIntentId) &&
            !string.Equals(order.AuthorizationTransactionId, session.PaymentIntentId, StringComparison.Ordinal))
        {
            order.AuthorizationTransactionId = session.PaymentIntentId;
            order.AuthorizationTransactionResult = session.Status;
            await _orderService.UpdateOrderAsync(order);
        }

        await _logger.InformationAsync(
            $"[Stripe BNPL] Checkout Session {session.Id} created for order {order.CustomOrderNumber} and provider {provider}.");

        return ToResult(session);
    }

    public async Task<StripeBnplCheckoutResult> GetAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Stripe Checkout Session id is required.", nameof(sessionId));

        _environmentGuard.EnsureSafe();
        var session = await _sessionClient.GetAsync(sessionId,
            new SessionGetOptions { Expand = new List<string> { "payment_intent" } });
        return ToResult(session);
    }

    private async Task<List<SessionLineItemOptions>> BuildLineItemsAsync(Order order, IList<OrderItem> orderItems,
        long expectedTotalMinor, string currency)
    {
        var lines = new List<(string Name, long Amount, Dictionary<string, string> Metadata)>();
        foreach (var item in orderItems)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            var customerAmount = _currencyService.ConvertCurrency(item.PriceInclTax, order.CurrencyRate);
            var itemMinor = ToMinorUnits(customerAmount, currency);
            if (itemMinor <= 0)
                continue;

            lines.Add((
                Truncate($"{product?.Name ?? "Product"} × {item.Quantity}", 120),
                itemMinor,
                new Dictionary<string, string>
                {
                    ["product_id"] = item.ProductId.ToString(),
                    ["order_item_id"] = item.Id.ToString(),
                    ["quantity"] = item.Quantity.ToString()
                }));
        }

        var shippingMinor = ToMinorUnits(
            _currencyService.ConvertCurrency(order.OrderShippingInclTax, order.CurrencyRate), currency);
        if (shippingMinor > 0)
            lines.Add(("Shipping", shippingMinor, new Dictionary<string, string> { ["component"] = "shipping" }));

        var currentTotal = lines.Sum(line => line.Amount);
        if (currentTotal > expectedTotalMinor)
        {
            var difference = currentTotal - expectedTotalMinor;
            while (difference > 0)
            {
                var sourceIndex = lines.FindIndex(item => item.Amount == lines.Max(candidate => candidate.Amount));
                if (sourceIndex < 0 || lines[sourceIndex].Amount <= 1)
                    break;

                var line = lines[sourceIndex];
                var reduction = Math.Min(difference, line.Amount - 1);
                lines[sourceIndex] = (line.Name, line.Amount - reduction, line.Metadata);
                difference -= reduction;
            }

            if (difference > 0)
                lines.Clear();
        }

        currentTotal = lines.Sum(line => line.Amount);
        if (currentTotal < expectedTotalMinor)
        {
            lines.Add(("Tax, discounts and order adjustments", expectedTotalMinor - currentTotal,
                new Dictionary<string, string>
                {
                    ["component"] = "order_adjustment",
                    ["tax_minor"] = ToMinorUnits(
                        _currencyService.ConvertCurrency(order.OrderTax, order.CurrencyRate), currency).ToString(),
                    ["discount_minor"] = ToMinorUnits(
                        _currencyService.ConvertCurrency(order.OrderDiscount + order.OrderSubTotalDiscountInclTax,
                            order.CurrencyRate), currency).ToString()
                }));
        }

        if (lines.Count == 0)
            lines.Add(($"Order {order.CustomOrderNumber ?? order.Id.ToString()}", expectedTotalMinor,
                new Dictionary<string, string> { ["component"] = "order_total" }));

        if (lines.Sum(line => line.Amount) != expectedTotalMinor)
            throw new NopException("Unable to allocate the nopCommerce order total to Stripe Checkout line items.");

        return lines.Select(line => new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                UnitAmount = line.Amount,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = line.Name,
                    Metadata = line.Metadata
                }
            }
        }).ToList();
    }

    private async Task<ChargeShippingOptions> ToStripeShippingAsync(NopAddress address)
    {
        if (address == null)
            return null;

        var country = address.CountryId.HasValue
            ? await _countryService.GetCountryByIdAsync(address.CountryId.Value)
            : null;
        var state = address.StateProvinceId.HasValue
            ? await _stateProvinceService.GetStateProvinceByIdAsync(address.StateProvinceId.Value)
            : null;

        return new ChargeShippingOptions
        {
            Name = string.Join(' ', new[] { address.FirstName, address.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
            Phone = address.PhoneNumber,
            Address = new AddressOptions
            {
                Line1 = address.Address1,
                Line2 = address.Address2,
                City = address.City,
                State = state?.Abbreviation ?? state?.Name ?? address.County,
                PostalCode = address.ZipPostalCode,
                Country = country?.TwoLetterIsoCode
            }
        };
    }

    internal static long ToMinorUnits(decimal amount, string currency)
    {
        var exponent = currency?.ToUpperInvariant() switch
        {
            "BIF" or "CLP" or "DJF" or "GNF" or "JPY" or "KMF" or "KRW" or "MGA" or "PYG" or
                "RWF" or "UGX" or "VND" or "VUV" or "XAF" or "XOF" or "XPF" => 0,
            "BHD" or "JOD" or "KWD" or "OMR" or "TND" => 3,
            _ => 2
        };
        var factor = exponent switch { 0 => 1m, 3 => 1000m, _ => 100m };
        return checked((long)decimal.Round(amount * factor, 0, MidpointRounding.AwayFromZero));
    }

    internal static decimal FromMinorUnits(long amount, string currency)
    {
        var exponent = currency?.ToUpperInvariant() switch
        {
            "BIF" or "CLP" or "DJF" or "GNF" or "JPY" or "KMF" or "KRW" or "MGA" or "PYG" or
                "RWF" or "UGX" or "VND" or "VUV" or "XAF" or "XOF" or "XPF" => 0,
            "BHD" or "JOD" or "KWD" or "OMR" or "TND" => 3,
            _ => 2
        };
        var factor = exponent switch { 0 => 1m, 3 => 1000m, _ => 100m };
        return amount / factor;
    }

    private static StripeBnplCheckoutResult ToResult(Session session) =>
        new(session.Id, session.Url, session.PaymentIntentId, session.AmountTotal ?? 0, session.Currency,
            session.Status);

    private static bool MetadataMatches(Dictionary<string, string> metadata, Order order, BnplProvider provider,
        string eligibilitySnapshotHash) =>
        metadata != null &&
        metadata.TryGetValue("order_id", out var orderId) && orderId == order.Id.ToString() &&
        metadata.TryGetValue("order_guid", out var orderGuid) &&
        string.Equals(orderGuid, order.OrderGuid.ToString("D"), StringComparison.OrdinalIgnoreCase) &&
        metadata.TryGetValue("provider", out var providerSlug) &&
        string.Equals(providerSlug, StripeBnplDefaults.GetProviderSlug(provider), StringComparison.OrdinalIgnoreCase) &&
        metadata.TryGetValue("eligibility_snapshot_hash", out var capturedHash) &&
        string.Equals(capturedHash, eligibilitySnapshotHash, StringComparison.OrdinalIgnoreCase);

    private static void EnsureTrustedCheckoutUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "checkout.stripe.com", StringComparison.OrdinalIgnoreCase))
            throw new NopException("Stripe returned an untrusted Checkout redirect URL.");
    }

    internal static string BuildIntegrationIdentifier(BnplProvider provider, Guid orderGuid)
    {
        var seed = Encoding.UTF8.GetBytes($"{orderGuid:N}:{(int)provider}");
        var hash = SHA256.HashData(seed);
        var suffix = new string(hash.Take(8).Select(value => (char)('a' + value % 26)).ToArray());
        return $"nop_bnpl_{StripeBnplDefaults.GetProviderSlug(provider)}_{suffix}";
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
