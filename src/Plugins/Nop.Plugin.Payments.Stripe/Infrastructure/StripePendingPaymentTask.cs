using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.ScheduleTasks;
using Nop.Web.Areas.Admin.Factories;
using Stripe;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.Payments.Stripe.Infrastructure;
// Infrastructure/StripePendingPaymentTask.cs
public class StripePendingPaymentTask : IScheduleTask
{
    private readonly IOrderService _orderService;
    private readonly StripePaymentProcessor _paymentProcessor;
    private readonly ILogger _logger;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly IOrderModelFactory _orderModelFactory;
    public StripePendingPaymentTask(
        IOrderService orderService,
        StripePaymentProcessor paymentProcessor,
        ILogger logger,
        IWorkContext workContext,
        IStoreContext storeContext,IOrderModelFactory orderModelFactory)
    {
        _orderService = orderService;
        _paymentProcessor = paymentProcessor;
        _logger = logger;
        _workContext = workContext;
        _storeContext = storeContext;
        _orderModelFactory = orderModelFactory;
    }

    public async Task ExecuteAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        OrderSearchModel searchModel = new OrderSearchModel();

        searchModel.OrderStatusIds.Add((int)OrderStatus.Pending);


        searchModel.PaymentStatusIds.Add((int)PaymentStatus.Pending);
        searchModel.PaymentMethodSystemName = "Payments.Stripe";

            //prepare model
            var pendingOrders = await _orderModelFactory.PrepareOrderListModelAsync(searchModel);
          

        foreach (var orderModel in pendingOrders.Data)
        {
            var order = await _orderService.GetOrderByIdAsync(orderModel.Id);
            try
            {
             
                await _paymentProcessor.ConfirmPendingPaymentIntentAsync(order);
                await _logger.InformationAsync($"[Stripe] Order {order.CustomOrderNumber} payment confirmed", customer: await _workContext.GetCurrentCustomerAsync());
            }
            catch (StripeException ex)
            {
                await HandleStripeError(order, ex);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"[Stripe] Error confirming payment for order {order.CustomOrderNumber}: {ex.Message}", ex);
            }
        }
    }

    private async Task HandleStripeError(Order order, StripeException ex)
    {
        var errorMessage = ex.StripeError?.Message ?? ex.Message;
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = $"Payment confirmation failed: {errorMessage}",
            DisplayToCustomer = true,
            CreatedOnUtc = DateTime.UtcNow
        });

        await _logger.ErrorAsync($"[Stripe] Payment error for order {order.CustomOrderNumber}: {errorMessage}", ex);
    }
}
