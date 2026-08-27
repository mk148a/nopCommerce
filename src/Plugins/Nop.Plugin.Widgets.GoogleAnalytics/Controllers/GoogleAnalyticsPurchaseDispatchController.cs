using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Widgets.GoogleAnalytics.Services;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Controllers;

/// <summary>
/// Confirms that the completed-page browser accepted a leased purchase event.
/// The time-bounded, high-entropy lease token is posted as a capability together
/// with ASP.NET Core antiforgery form data. Current customer ownership is checked
/// again so it cannot confirm another customer's order.
/// </summary>
public class GoogleAnalyticsPurchaseDispatchController : BasePluginController
{
    private readonly IOrderService _orderService;
    private readonly IGoogleAnalyticsPurchaseDispatchService _purchaseDispatchService;
    private readonly IWorkContext _workContext;

    public GoogleAnalyticsPurchaseDispatchController(IOrderService orderService,
        IGoogleAnalyticsPurchaseDispatchService purchaseDispatchService,
        IWorkContext workContext)
    {
        _orderService = orderService;
        _purchaseDispatchService = purchaseDispatchService;
        _workContext = workContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm([FromForm] PurchaseDispatchConfirmationRequest request)
    {
        if (request == null || request.OrderId <= 0 || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest();

        var order = await _orderService.GetOrderByIdAsync(request.OrderId);
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (order == null || order.Deleted || order.CustomerId != customer.Id)
            return NotFound();

        return await _purchaseDispatchService.TryConfirmAsync(request.OrderId, request.Token)
            ? NoContent()
            : NotFound();
    }
}

public class PurchaseDispatchConfirmationRequest
{
    public int OrderId { get; set; }

    public string Token { get; set; }
}
