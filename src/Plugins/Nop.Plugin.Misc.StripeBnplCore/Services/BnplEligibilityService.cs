using Nop.Core.Domain.Orders;
using Nop.Core;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class BnplEligibilityService : IBnplEligibilityService
{
    private readonly IBnplCatalogRiskService _catalogRiskService;
    private readonly IBnplCartContextService _cartContextService;
    private readonly IBnplProductEligibilityStore _eligibilityStore;
    private readonly IStripeBnplEnvironmentGuard _environmentGuard;
    private readonly StripeBnplSettings _settings;

    public BnplEligibilityService(
        IBnplCatalogRiskService catalogRiskService,
        IBnplCartContextService cartContextService,
        IBnplProductEligibilityStore eligibilityStore,
        IStripeBnplEnvironmentGuard environmentGuard,
        StripeBnplSettings settings)
    {
        _catalogRiskService = catalogRiskService;
        _cartContextService = cartContextService;
        _eligibilityStore = eligibilityStore;
        _environmentGuard = environmentGuard;
        _settings = settings;
    }

    public async Task<BnplCartEligibilityResult> EvaluateCartAsync(BnplProvider provider, IList<ShoppingCartItem> cart)
    {
        var context = cart == null ? null : await _cartContextService.GetAsync(cart);
        return await EvaluateAsync(provider, cart, context);
    }

    public async Task<BnplCartEligibilityResult> EvaluateOrderAsync(BnplProvider provider, Order order,
        IList<OrderItem> orderItems)
    {
        if (order == null || orderItems == null)
            return BnplCartEligibilityResult.Blocked("order_missing", "Order snapshot is unavailable.");
        var cart = orderItems.Select(item => new ShoppingCartItem
        {
            CustomerId = order.CustomerId,
            StoreId = order.StoreId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            AttributesXml = item.AttributesXml,
            ShoppingCartType = ShoppingCartType.ShoppingCart
        }).ToList();
        return await EvaluateAsync(provider, cart, await _cartContextService.GetAsync(order));
    }

    public async Task<StripeBnplEligibilitySnapshotEnvelope> CaptureOrderSnapshotAsync(BnplProvider provider,
        Order order, IList<OrderItem> orderItems, long amountMinor, string currency)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(orderItems);
        if (amountMinor <= 0 || string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("A positive amount and currency are required for an eligibility snapshot.");

        // Re-evaluate immediately before capture so a concurrent administrative
        // change cannot slip a non-Allowed row into a newly reserved attempt.
        var eligibility = await EvaluateOrderAsync(provider, order, orderItems);
        if (!eligibility.IsEligible)
            throw new NopException(
                $"Stripe BNPL eligibility changed before snapshot capture ({eligibility.ReasonCode}): {eligibility.Reason}");

        var context = await _cartContextService.GetAsync(order)
            ?? throw new NopException("Order eligibility context is unavailable for snapshot capture.");
        var records = await _eligibilityStore.GetByProductIdsAsync(
            orderItems.Select(item => item.ProductId).Distinct().ToArray(), provider);
        var recordByProduct = records.GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedOnUtc).First());
        var items = new List<StripeBnplEligibilitySnapshotItem>(orderItems.Count);

        foreach (var orderItem in orderItems.OrderBy(item => item.Id).ThenBy(item => item.ProductId))
        {
            if (!recordByProduct.TryGetValue(orderItem.ProductId, out var record) ||
                record.EligibilityState != BnplEligibilityState.Allowed)
                throw new NopException("A non-Allowed or missing product matrix row was detected during snapshot capture.");

            var afterpayFulfillment = ValidateAfterpayFulfillment(provider, record, orderItem.ProductId);
            if (afterpayFulfillment != null)
                throw new NopException(
                    $"Stripe BNPL eligibility changed before snapshot capture ({afterpayFulfillment.ReasonCode}): {afterpayFulfillment.Reason}");

            var restrictedTerm = await _catalogRiskService.FindRestrictedTermAsync(
                orderItem.ProductId, orderItem.AttributesXml);
            items.Add(new StripeBnplEligibilitySnapshotItem(
                orderItem.Id,
                orderItem.ProductId,
                orderItem.Quantity,
                orderItem.AttributesXml ?? string.Empty,
                record.Id,
                record.EligibilityState,
                record.Reason,
                record.FulfillmentDays,
                record.ApprovalReference,
                record.PolicyVersion,
                record.UpdatedOnUtc,
                restrictedTerm));
        }

        return StripeBnplEligibilitySnapshotCodec.Create(new StripeBnplEligibilitySnapshot(
            StripeBnplEligibilitySnapshotCodec.CurrentVersion,
            order.Id,
            order.OrderGuid,
            provider,
            context.CustomerCountryIso2,
            currency.Trim().ToLowerInvariant(),
            amountMinor,
            items));
    }

    private async Task<BnplCartEligibilityResult> EvaluateAsync(BnplProvider provider, IList<ShoppingCartItem> cart,
        BnplCartContext context)
    {
        if (!Enum.IsDefined(provider))
            return BnplCartEligibilityResult.Blocked("provider_invalid", "The BNPL provider is not recognized.");

        if (cart == null || cart.Count == 0)
            return BnplCartEligibilityResult.Blocked("cart_empty", "An empty cart is not eligible.");

        var environment = _environmentGuard.Check();
        if (!environment.IsSafe)
            return BnplCartEligibilityResult.Blocked("environment_unsafe", environment.Reason,
                cart.Select(item => item.ProductId));

        if (string.IsNullOrWhiteSpace(_settings.GetActiveRestrictedKey()))
            return BnplCartEligibilityResult.Blocked("api_key_missing", "Stripe restricted API key is not configured.");

        if (string.IsNullOrWhiteSpace(_settings.GetPaymentMethodConfigurationId(provider)))
            return BnplCartEligibilityResult.Blocked("payment_configuration_missing",
                "Stripe Payment Method Configuration is not configured for this provider.");

        // This is deliberately not a configurable toggle. A copied database or an
        // old setting value must not be able to expose a BNPL method in live mode
        // before provider-specific written approval has been recorded.
        if (!_settings.UseSandbox && string.IsNullOrWhiteSpace(_settings.GetApprovalReference(provider)))
            return BnplCartEligibilityResult.Blocked("live_approval_missing",
                "A written provider approval reference is required before live checkout can be shown.");

        if (context == null)
            return BnplCartEligibilityResult.Blocked("cart_context_unknown",
                "Customer country, currency, or final cart total is not known.", cart.Select(item => item.ProductId));

        // Stripe is the authority for buyer-country, currency, amount and credit
        // eligibility. The checkout session receives the final address, currency
        // and total; Stripe's dynamic payment-method engine decides whether the
        // selected provider can actually be offered. Do not duplicate or freeze
        // Stripe's country/amount matrix locally.

        var productIds = cart.Select(item => item.ProductId).Distinct().ToArray();
        var records = await _eligibilityStore.GetByProductIdsAsync(productIds, provider);

        var recordByProduct = records.GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedOnUtc).First());

        foreach (var item in cart)
        {
            if (!recordByProduct.TryGetValue(item.ProductId, out var record))
                return BnplCartEligibilityResult.Blocked("product_unknown",
                    "Every product must be explicitly approved for this provider.", new[] { item.ProductId });

            if (record.EligibilityState != BnplEligibilityState.Allowed)
                return BnplCartEligibilityResult.Blocked($"product_{record.EligibilityState.ToString().ToLowerInvariant()}",
                    string.IsNullOrWhiteSpace(record.Reason)
                        ? "The product is not approved for this provider."
                        : record.Reason,
                    new[] { item.ProductId });

            var afterpayFulfillment = ValidateAfterpayFulfillment(provider, record, item.ProductId);
            if (afterpayFulfillment != null)
                return afterpayFulfillment;

            var restrictedTerm = await _catalogRiskService.FindRestrictedTermAsync(item.ProductId, item.AttributesXml);
            if (restrictedTerm != null && string.IsNullOrWhiteSpace(record.ApprovalReference))
                return BnplCartEligibilityResult.Blocked("restricted_catalog_term",
                    $"Product catalog text contains restricted term '{restrictedTerm}' and has no written exception reference.",
                    new[] { item.ProductId });
        }

        return BnplCartEligibilityResult.Eligible();
    }

    private BnplCartEligibilityResult ValidateAfterpayFulfillment(BnplProvider provider,
        BnplProductEligibility record, int productId)
    {
        if (provider != BnplProvider.Afterpay)
            return null;

        if (_settings.AfterpayMaximumFulfillmentDays < 0)
            return BnplCartEligibilityResult.Blocked("afterpay_fulfillment_configuration_invalid",
                "Afterpay maximum fulfillment days cannot be negative.", new[] { productId });

        if (record.FulfillmentDays is < 0)
            return BnplCartEligibilityResult.Blocked("afterpay_fulfillment_invalid",
                "Afterpay fulfillment time cannot be negative.", new[] { productId });

        // Stripe's account-level activation review is authoritative. A merchant
        // can still apply a stricter local cap; zero means no additional local
        // fulfillment restriction.
        var afterpayMaximumDays = _settings.AfterpayMaximumFulfillmentDays;
        if (afterpayMaximumDays > 0 &&
            (!record.FulfillmentDays.HasValue || record.FulfillmentDays.Value > afterpayMaximumDays))
            return BnplCartEligibilityResult.Blocked("afterpay_fulfillment_window",
                $"Afterpay requires a known fulfillment time of {afterpayMaximumDays} days or less.",
                new[] { productId });

        return null;
    }

}
