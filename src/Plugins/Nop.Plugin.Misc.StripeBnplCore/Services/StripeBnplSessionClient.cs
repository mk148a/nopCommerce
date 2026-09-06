using Nop.Core;
using Stripe;
using Stripe.Checkout;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplSessionClient : IStripeBnplSessionClient
{
    private readonly StripeBnplSettings _settings;

    public StripeBnplSessionClient(StripeBnplSettings settings)
    {
        _settings = settings;
    }

    public async Task<Session> CreateAsync(SessionCreateOptions options, RequestOptions requestOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        var service = new SessionService(CreateClient());
        return await service.CreateAsync(options, requestOptions);
    }

    public async Task<Session> GetAsync(string sessionId, SessionGetOptions options = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Stripe Checkout Session id is required.", nameof(sessionId));

        var service = new SessionService(CreateClient());
        return await service.GetAsync(sessionId, options);
    }

    private StripeClient CreateClient()
    {
        var apiKey = _settings.GetActiveRestrictedKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new NopException("Stripe restricted API key is not configured.");

        return new StripeClient(apiKey.Trim());
    }
}
