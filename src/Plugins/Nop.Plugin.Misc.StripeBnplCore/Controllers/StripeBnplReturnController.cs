using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.StripeBnplCore.Services;
using Nop.Plugin.Misc.StripeBnplCore.Models;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.StripeBnplCore.Controllers;

public sealed class StripeBnplReturnController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IStripeBnplPaymentFinalizer _finalizer;
    private readonly ILogger _logger;

    public StripeBnplReturnController(
        IOrderService orderService,
        IStripeBnplPaymentFinalizer finalizer,
        ILogger logger)
    {
        _orderService = orderService;
        _finalizer = finalizer;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Return(string session_id, Guid? order_guid)
    {
        try
        {
            var result = await _finalizer.FinalizeSessionAsync(session_id, order_guid, "browser_return");
            if (result.IsPaid)
                return RedirectToRoute("CheckoutCompleted", new { orderId = result.OrderId });

            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
            return View("~/Plugins/Misc.StripeBnplCore/Views/Pending.cshtml", new StripeBnplPendingModel
            {
                SessionId = session_id,
                OrderGuid = order_guid ?? Guid.Empty,
                StatusUrl = Url.RouteUrl(StripeBnplDefaults.StatusRouteName),
                OrderDetailsUrl = Url.RouteUrl("OrderDetails", new { orderId = result.OrderId })
            });
        }
        catch (Exception exception)
        {
            await _logger.ErrorAsync("[Stripe BNPL] Secure return finalization failed.", exception);
            if (order_guid.HasValue && await _orderService.GetOrderByGuidAsync(order_guid.Value) is { } order)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            return NotFound();
        }
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(string session_id, Guid? order_guid)
    {
        if (string.IsNullOrWhiteSpace(session_id) || !order_guid.HasValue || order_guid == Guid.Empty)
            return BadRequest(new { status = "invalid" });

        try
        {
            var result = await _finalizer.FinalizeSessionAsync(session_id, order_guid, "browser_poll");
            if (result.IsPaid)
                return Json(new
                {
                    status = "paid",
                    redirect_url = Url.RouteUrl("CheckoutCompleted", new { orderId = result.OrderId })
                });

            var terminal = result.Status?.ToLowerInvariant() is
                "expired" or "canceled" or "cancelled" or "payment_failed" or "unpaid";
            return Json(new
            {
                status = terminal ? "failed" : "pending",
                redirect_url = terminal
                    ? Url.RouteUrl("OrderDetails", new { orderId = result.OrderId })
                    : null
            });
        }
        catch (Exception exception)
        {
            // A temporary Stripe/API or concurrent-finalization failure must be
            // retried by the browser. Identity mismatches are still logged and no
            // order data is returned to the caller.
            await _logger.WarningAsync($"[Stripe BNPL] Pending status check deferred: {exception.Message}");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "pending" });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(Guid? order_guid)
    {
        if (order_guid.HasValue && await _orderService.GetOrderByGuidAsync(order_guid.Value) is { } order)
            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        return NotFound();
    }
}
