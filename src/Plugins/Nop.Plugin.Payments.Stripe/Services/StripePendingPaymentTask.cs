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
using Nop.Core.Domain.Messages;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Token = Stripe.Token;

namespace Nop.Plugin.Payments.Stripe.Services;

public class StripePendingPaymentTask : IScheduleTask
{
    private readonly IOrderService _orderService;
    private readonly ILogger _logger;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly IOrderModelFactory _orderModelFactory;
    private readonly ICustomerService _customerService;
    private readonly IMessageTokenProvider _messageTokenProvider;
    private readonly ILocalizationService _localizationService;
    private readonly StripePaymentSettings _stripePaymentSettings;
    protected readonly ITokenizer _tokenizer;
    private readonly IQueuedEmailService _queuedEmailService;
    private readonly IEmailAccountService _emailAccountService;
    private readonly EmailAccountSettings _emailAccountSettings;


    public StripePendingPaymentTask(
        IOrderService orderService,
        ILogger logger,
        IWorkContext workContext,
        IStoreContext storeContext, IOrderModelFactory orderModelFactory,
        ICustomerService customerService,IMessageTokenProvider messageTokenProvider,
        ILocalizationService localizationService, StripePaymentSettings stripePaymentSettings,
        ITokenizer tokenizer,IQueuedEmailService queuedEmailService,
            IEmailAccountService emailAccountService, EmailAccountSettings emailAccountSettings )
    {
        _orderService = orderService;
        _logger = logger;
        _workContext = workContext;
        _storeContext = storeContext;
        _orderModelFactory = orderModelFactory;
        _customerService = customerService;
        _messageTokenProvider = messageTokenProvider;
        _localizationService = localizationService;
        _stripePaymentSettings = stripePaymentSettings;
        _tokenizer = tokenizer;
        _queuedEmailService = queuedEmailService;
        _emailAccountService = emailAccountService;
        _emailAccountSettings = emailAccountSettings;
    }


    /// <summary>
    /// Set up for a call to the Stripe API
    /// </summary>
    /// <returns></returns>
    private RequestOptions GetStripeApiRequestOptions()
    {
        return new RequestOptions
        {
            ApiKey = _stripePaymentSettings.SecretKey,
            IdempotencyKey = Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Siparişe yeni bir not ekler.
    /// </summary>
    /// <param name="order">Sipariş</param>
    /// <param name="note">Eklenecek not</param>
    /// <param name="displayToCustomer">Müşteriye gösterilsin mi?</param>
    /// <returns></returns>
    private async Task CreateOrderNote(Order order, string note, bool displayToCustomer = true)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        if (string.IsNullOrEmpty(note))
            throw new ArgumentNullException(nameof(note));

        // Yeni sipariş notu oluştur
        var orderNote = new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = displayToCustomer,
            CreatedOnUtc = DateTime.UtcNow
        };

        // Sipariş notunu veritabanına ekle
        await _orderService.InsertOrderNoteAsync(orderNote);

        // Loglama yap
        await _logger.InformationAsync($"[Stripe] Order note added for order {order.CustomOrderNumber}: {note}");
    }
    /// <summary>
    /// Müşteriye e-posta gönderir.
    /// </summary>
    /// <param name="order">Sipariş</param>
    /// <param name="messageTemplateName">E-posta şablonu adı</param>
    /// <param name="tokens">E-posta içeriğinde kullanılacak token'lar</param>
    /// <returns></returns>
    /// <summary>
    /// Müşteriye e-posta gönderir.
    /// </summary>
    /// <param name="order">Sipariş</param>
    /// <param name="messageTemplateName">E-posta şablonu adı</param>
    /// <param name="tokens">E-posta içeriğinde kullanılacak token'lar</param>
    /// <returns></returns>
    private async Task SendCustomerEmail(Order order, string messageTemplateName, IEnumerable<Token> tokens = null)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        if (string.IsNullOrEmpty(messageTemplateName))
            throw new ArgumentNullException(nameof(messageTemplateName));

        // Müşteri bilgilerini al
        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
        if (customer == null)
            throw new NopException($"Customer not found for order {order.CustomOrderNumber}");

        // Dil ve mağaza bilgilerini al
        var languageId = customer.LanguageId ?? (await _workContext.GetWorkingLanguageAsync()).Id;
        var store = await _storeContext.GetCurrentStoreAsync();

        // Token listesini hazırla (varsayılan token'ları ekle)
        var defaultTokens = new List<Nop.Services.Messages.Token>
    {
        new ("Order.CustomerFullName", customer.FirstName+" "+customer.LastName),
        new ("Order.CustomerEmail", customer.Email),
        new ("Order.OrderNumber", order.CustomOrderNumber),
        new ("Store.Name", store.Name),
        new ("Store.URL", store.Url)
    };



        // Sipariş token'larını ekle
        await _messageTokenProvider.AddOrderTokensAsync(defaultTokens, order, languageId);

        // E-posta şablonunu al
        var subjectTemplate = await _localizationService.GetResourceAsync($"Plugins.Payments.Stripe.EmailTemplates.{messageTemplateName}.Subject", languageId);
        var bodyTemplate = await _localizationService.GetResourceAsync($"Plugins.Payments.Stripe.EmailTemplates.{messageTemplateName}.Body", languageId);

        // Token'ları şablona uygula
        var subject = _tokenizer.Replace(subjectTemplate, defaultTokens, false);
        var body = _tokenizer.Replace(bodyTemplate, defaultTokens, true);

        // E-posta kuyruğuna ekle
        var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
        if (emailAccount == null)
        {
            await _logger.WarningAsync($"Stripe pending-payment email skipped for order {order.CustomOrderNumber}: no email account is configured.");
            return;
        }
        var email = new QueuedEmail
        {
            Priority = QueuedEmailPriority.High,
            From = emailAccount.Email,
            FromName = emailAccount.DisplayName,
            To = customer.Email,
            Subject = subject,
            Body = body,
            CreatedOnUtc = DateTime.UtcNow,
            EmailAccountId = emailAccount.Id
        };

        await _queuedEmailService.InsertQueuedEmailAsync(email);

        // Loglama yap
        await _logger.InformationAsync($"[Stripe] Email sent to customer {customer.Email} for order {order.CustomOrderNumber} using template {messageTemplateName}");
    }

    private async Task ConfirmPaymentIntent(PaymentIntent paymentIntent, Order order)
    {
        var service = new PaymentIntentService();
        var confirmedIntent = await service.ConfirmAsync(paymentIntent.Id, null, GetStripeApiRequestOptions());

        if (confirmedIntent.Status == "succeeded")
        {
            await UpdateOrderStatus(order, PaymentStatus.Paid, OrderStatus.Processing);
            await CreateOrderNote(order, "Payment automatically confirmed by system");
        }
    }

    private async Task HandleRequiresAction(PaymentIntent paymentIntent, Order order)
    {
        await CreateOrderNote(order, "Payment requires additional action. Customer should check their email for instructions.");
        await SendCustomerEmail(order, "PaymentActionRequired");
    }

    private async Task HandleUnsuccessfulPaymentIntent(PaymentIntent paymentIntent, Order order)
    {
        // Stripe has no chargeable payment method (or the intent was cancelled).
        // Stop retrying the pending order instead of turning an expected payment
        // failure into an unhandled application error on every scheduled run.
        await UpdateOrderStatus(order, PaymentStatus.Voided, OrderStatus.Cancelled);
        await CreateOrderNote(order,
            $"Stripe payment was not completed (status: {paymentIntent.Status}). The order was cancelled; the customer can place a new order with another payment method.");
        await _logger.WarningAsync($"[Stripe] Payment intent {paymentIntent.Status}; order {order.CustomOrderNumber} cancelled without retry.");
    }

    private async Task UpdateOrderStatus(Order order, PaymentStatus paymentStatus, OrderStatus orderStatus)
    {
        order.PaymentStatus = paymentStatus;
        order.OrderStatus = orderStatus;
        await _orderService.UpdateOrderAsync(order);
    }
    public async Task ConfirmPendingPaymentIntentAsync(Order order)
    {
        if (string.IsNullOrEmpty(order.AuthorizationTransactionId))
            throw new NopException("No authorization transaction ID found");

        var service = new PaymentIntentService();
        var paymentIntent = await service.GetAsync(order.AuthorizationTransactionId, null, GetStripeApiRequestOptions());

        switch (paymentIntent.Status)
        {
            case "requires_confirmation":
                await ConfirmPaymentIntent(paymentIntent, order);
                break;
            case "requires_action":
                await HandleRequiresAction(paymentIntent, order);
                break;
            case "succeeded":
                await UpdateOrderStatus(order, PaymentStatus.Paid, OrderStatus.Processing);
                break;
            case "requires_payment_method":
            case "canceled":
                await HandleUnsuccessfulPaymentIntent(paymentIntent, order);
                break;
            default:
                throw new NopException($"Unhandled payment status: {paymentIntent.Status}");
        }
    }
    public async Task ExecuteAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var searchModel = new OrderSearchModel();

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

                await ConfirmPendingPaymentIntentAsync(order);
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
