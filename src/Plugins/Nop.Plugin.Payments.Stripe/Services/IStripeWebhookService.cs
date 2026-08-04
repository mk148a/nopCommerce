using System;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Stripe.Services;

public interface IStripeWebhookService
{
    Task ProcessAsync(string payload, string signature);

    Task<StripeWebhookSyncResult> SyncEndpointAsync();
}

public sealed class StripeWebhookSyncResult
{
    public string EndpointId { get; init; }

    public string EndpointUrl { get; init; }

    public string EndpointStatus { get; init; }

    public bool Created { get; init; }

    public bool Updated { get; init; }

    public bool SigningSecretSaved { get; init; }

    public bool SigningSecretRequired { get; init; }

    public int MatchingEndpointCount { get; init; }
}

public sealed class StripeWebhookValidationException : Exception
{
    public StripeWebhookValidationException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }
}
