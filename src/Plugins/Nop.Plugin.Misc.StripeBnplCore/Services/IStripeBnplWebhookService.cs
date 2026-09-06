namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplWebhookService
{
    Task ProcessAsync(string payload, string signature);
}

public sealed class StripeBnplWebhookValidationException : Exception
{
    public StripeBnplWebhookValidationException(string message) : base(message)
    {
    }

    public StripeBnplWebhookValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class StripeBnplWebhookRetryException : Exception
{
    public StripeBnplWebhookRetryException(string message) : base(message)
    {
    }
}
