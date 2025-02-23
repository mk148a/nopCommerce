using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Plugin.Misc.GoogleBotAggregator.Services;
using Nop.Services.Customers;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

/// <summary>
/// Represents middleware that checks for Google Bot requests
/// </summary>
public class GoogleBotMiddleware
{
    #region Fields

    private readonly RequestDelegate _next;
    private readonly IGoogleBotService _googleBotService;
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    #endregion

    #region Ctor

    public GoogleBotMiddleware(
        RequestDelegate next,
        IGoogleBotService googleBotService,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _next = next;
        _googleBotService = googleBotService;
        _workContext = workContext;
        _customerService = customerService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke middleware actions
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        
        if (await _googleBotService.IsGoogleBotAsync(userAgent))
        {
            var currentCustomer = await _workContext.GetCurrentCustomerAsync();
            var googleBotCustomer = await _googleBotService.GetOrCreateGoogleBotCustomerAsync();

            if (currentCustomer.Id != googleBotCustomer.Id)
            {
                // Sepetleri birleştir
                await _googleBotService.MergeShoppingCartsAsync(currentCustomer.Id, googleBotCustomer.Id);

                // Müşteriyi değiştir
                await _workContext.SetCurrentCustomerAsync(googleBotCustomer);
            }
        }

        await _next(context);
    }

    #endregion
} 