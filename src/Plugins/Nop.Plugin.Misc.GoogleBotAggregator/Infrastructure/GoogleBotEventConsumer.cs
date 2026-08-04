using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Core.Events;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

/// <summary>
/// Google Bot event consumer
/// </summary>
public class GoogleBotEventConsumer : IConsumer<EntityInsertedEvent<ActivityLog>>
{
    #region Fields

    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ICustomerService _customerService;
    private readonly GoogleBotAggregatorSettings _settings;

    #endregion

    #region Ctor

    public GoogleBotEventConsumer(
        IGenericAttributeService genericAttributeService,
        ICustomerService customerService,
        GoogleBotAggregatorSettings settings)
    {
        _genericAttributeService = genericAttributeService;
        _customerService = customerService;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle activity log event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityInsertedEvent<ActivityLog> eventMessage)
    {
        if (!_settings.ExcludeFromAnalytics)
            return;

        var activityLog = eventMessage.Entity;
        if (activityLog == null)
            return;

        var customer = await _customerService.GetCustomerByIdAsync(activityLog.CustomerId);
        if (customer == null)
            return;

        // Google Bot müşterisi ise aktiviteyi işaretle
        var isGoogleBot = await _genericAttributeService.GetAttributeAsync<bool>(customer, "IsGoogleBot");
        if (isGoogleBot)
        {
            // Yoruma [GoogleBot] işareti ekle
            activityLog.Comment = $"[GoogleBot] {activityLog.Comment}";
        }
    }

    #endregion
} 