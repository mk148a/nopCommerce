using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Core.Events;
using Nop.Services.Common;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

/// <summary>
/// Google Bot event consumer
/// </summary>
public class GoogleBotEventConsumer : IConsumer<EntityInsertedEvent<ActivityLog>>
{
    #region Fields

    private readonly IGenericAttributeService _genericAttributeService;
    private readonly GoogleBotAggregatorSettings _settings;

    #endregion

    #region Ctor

    public GoogleBotEventConsumer(
        IGenericAttributeService genericAttributeService,
        GoogleBotAggregatorSettings settings)
    {
        _genericAttributeService = genericAttributeService;
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
        if (activityLog?.CustomerId == null)
            return;

        var customer = await _genericAttributeService.GetAttributeAsync<Customer>(activityLog.CustomerId.Value, "Customer");
        if (customer == null)
            return;

        // Google Bot müşterisi ise aktiviteyi kaydetme
        var isGoogleBot = await _genericAttributeService.GetAttributeAsync<bool>(customer, "IsGoogleBot");
        if (isGoogleBot)
            activityLog.Enabled = false;
    }

    #endregion
} 