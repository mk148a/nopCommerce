using Microsoft.AspNetCore.Builder;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Infrastructure;

/// <summary>
/// Represents extensions for middleware registration
/// </summary>
public static class GoogleBotMiddlewareExtensions
{
    /// <summary>
    /// Use Google Bot middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public static void UseGoogleBot(this IApplicationBuilder application)
    {
        application.UseMiddleware<GoogleBotMiddleware>();
    }
} 