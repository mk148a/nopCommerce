using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.StripeBnplCore.Controllers;

public sealed class StripeBnplWebhookController : Controller
{
    private readonly IStripeBnplWebhookService _webhookService;
    private readonly ILogger _logger;

    public StripeBnplWebhookController(IStripeBnplWebhookService webhookService, ILogger logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
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
        catch (StripeBnplWebhookValidationException exception)
        {
            await _logger.WarningAsync($"[Stripe BNPL] Rejected webhook: {exception.Message}");
            return BadRequest();
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("[Stripe BNPL] Webhook processing failed.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
