using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.Stripe.Services;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.Stripe.Controllers;

/// <summary>
/// Anonymous Stripe endpoint. Signature verification is performed before any order work.
/// </summary>
public class StripeWebhookController : Controller
{
    private readonly IStripeWebhookService _webhookService;
    private readonly ILogger _logger;

    public StripeWebhookController(IStripeWebhookService webhookService, ILogger logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> WebhookHandler()
    {
        string payload;
        using (var reader = new StreamReader(Request.Body))
            payload = await reader.ReadToEndAsync();

        Request.Headers.TryGetValue("Stripe-Signature", out var signature);

        try
        {
            await _webhookService.ProcessAsync(payload, signature.ToString());
            return Ok();
        }
        catch (StripeWebhookValidationException exception)
        {
            // Invalid/missing signatures are client errors, not application failures. This
            // keeps Stripe retries and hostile requests out of the error-level log stream.
            await _logger.WarningAsync($"[Stripe] Rejected webhook request: {exception.Message}");
            return BadRequest();
        }
        catch (System.Exception exception)
        {
            await _logger.ErrorAsync("[Stripe] Webhook processing failed.", exception);
            return StatusCode(500);
        }
    }
}
