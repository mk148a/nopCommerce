using Nop.Core;
using Nop.Plugin.Misc.StripeBnplCore.Domain;
using Stripe;
using Microsoft.Extensions.Caching.Memory;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplConfigurationClient : IStripeBnplConfigurationClient
{
    private readonly StripeBnplSettings _settings;
    private readonly IMemoryCache _cache;

    public StripeBnplConfigurationClient(StripeBnplSettings settings, IMemoryCache cache)
    {
        _settings = settings;
        _cache = cache;
    }

    public async Task EnsureProviderOnlyAsync(BnplProvider provider, string configurationId, bool expectedLivemode)
    {
        var health = await CheckProviderOnlyAsync(provider, configurationId, expectedLivemode);
        if (!health.IsHealthy)
            throw new NopException(health.Reason);
    }

    public async Task<StripeBnplConfigurationHealth> CheckProviderOnlyAsync(BnplProvider provider,
        string configurationId, bool expectedLivemode)
    {
        if (string.IsNullOrWhiteSpace(configurationId) ||
            !configurationId.Trim().StartsWith("pmc_", StringComparison.Ordinal))
            return new(false, "A valid Stripe Payment Method Configuration id is required.");

        var cacheKey = $"stripe-bnpl-pmc:{expectedLivemode}:{(int)provider}:{configurationId.Trim()}";
        if (_cache.TryGetValue(cacheKey, out StripeBnplConfigurationHealth cached))
            return cached;

        StripeBnplConfigurationHealth result;
        try
        {
            var configuration = await new PaymentMethodConfigurationService(CreateClient())
                .GetAsync(configurationId.Trim());
            result = ValidateConfiguration(provider, configurationId.Trim(), expectedLivemode, configuration);
        }
        catch (Exception exception) when (exception is StripeException or NopException)
        {
            result = new(false, "Stripe Payment Method Configuration capability could not be verified.");
        }

        _cache.Set(cacheKey, result, result.IsHealthy ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(30));
        return result;
    }

    private static StripeBnplConfigurationHealth ValidateConfiguration(BnplProvider provider, string configurationId,
        bool expectedLivemode, PaymentMethodConfiguration configuration)
    {
        if (configuration == null ||
            !string.Equals(configuration.Id, configurationId, StringComparison.Ordinal) ||
            !configuration.Active || configuration.Livemode != expectedLivemode)
            return new(false, "Stripe Payment Method Configuration is inactive, missing, or in the wrong mode.");

        var selectedProperty = provider switch
        {
            BnplProvider.Klarna => nameof(PaymentMethodConfiguration.Klarna),
            BnplProvider.Affirm => nameof(PaymentMethodConfiguration.Affirm),
            BnplProvider.Afterpay => nameof(PaymentMethodConfiguration.AfterpayClearpay),
            BnplProvider.Zip => nameof(PaymentMethodConfiguration.Zip),
            _ => null
        };
        if (selectedProperty == null)
            return new(false, "Unknown BNPL provider.");

        var selectedFound = false;
        foreach (var property in typeof(PaymentMethodConfiguration).GetProperties())
        {
            var method = property.GetValue(configuration);
            if (method == null)
                continue;

            var display = method.GetType().GetProperty("DisplayPreference")?.GetValue(method);
            if (display == null)
                continue;

            var value = display.GetType().GetProperty("Value")?.GetValue(display) as string;
            var available = method.GetType().GetProperty("Available")?.GetValue(method) as bool? ?? false;
            var isSelected = string.Equals(property.Name, selectedProperty, StringComparison.Ordinal);
            if (isSelected)
            {
                selectedFound = available && string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
                return new(false,
                    $"Stripe configuration also enables {property.Name}; it must contain only {selectedProperty}.");
        }

        if (!selectedFound)
            return new(false, $"Stripe configuration does not make {selectedProperty} available and on.");
        return new(true, $"Stripe configuration enables only {selectedProperty}.");
    }

    private StripeClient CreateClient()
    {
        var key = _settings.GetActiveRestrictedKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new NopException("Stripe restricted API key is not configured.");
        return new StripeClient(key.Trim());
    }
}
