using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Nop.Plugin.Misc.HoodAuthShield.Infrastructure;

/// <summary>
/// Deliberately scopes protection to account-entry endpoints. Product, category, media,
/// sitemap, robots and all other crawl paths always pass through unchanged.
/// </summary>
public sealed class AuthShieldMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthBurstLimiter _limiter;

    public AuthShieldMiddleware(RequestDelegate next, AuthBurstLimiter limiter)
    {
        _next = next;
        _limiter = limiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsProtectedAuthPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Authentication entry points are functional routes, not search landing pages.
        // Set this before passing through so it also reaches explicitly allowed crawlers.
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (IsExplicitlyAllowedBot(context.Request.Headers.UserAgent))
        {
            await _next(context);
            return;
        }

        var clientKey = GetClientKey(context);
        if (_limiter.TryConsume(clientKey, DateTimeOffset.UtcNow, out var retryAfterSeconds))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsync("Too many account requests. Please try again shortly.");
    }

    private static bool IsProtectedAuthPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/login", StringComparison.OrdinalIgnoreCase)
               || value.Equals("/register", StringComparison.OrdinalIgnoreCase)
               || System.Text.RegularExpressions.Regex.IsMatch(value, "^/[a-z]{2}/(login|register)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)
               || System.Text.RegularExpressions.Regex.IsMatch(value, "^/[a-z]{2}/facebookauthentication/login$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static bool IsExplicitlyAllowedBot(string userAgent)
        => !string.IsNullOrWhiteSpace(userAgent)
           && (userAgent.Contains("GPTBot", StringComparison.OrdinalIgnoreCase)
           || userAgent.Contains("ChatGPT-User", StringComparison.OrdinalIgnoreCase)
           || userAgent.Contains("OAI-SearchBot", StringComparison.OrdinalIgnoreCase)
           || userAgent.Contains("Googlebot", StringComparison.OrdinalIgnoreCase));

    private static string GetClientKey(HttpContext context)
    {
        var cloudflareIp = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(cloudflareIp))
            return cloudflareIp.Trim();

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',', 2, StringSplitOptions.TrimEntries)[0];

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>Fixed one-minute per-client window with bounded in-memory state.</summary>
public sealed class AuthBurstLimiter
{
    private const int MaximumRequestsPerMinute = 12;
    private const int MaximumClientWindows = 50_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, WindowState> _windows = new(StringComparer.Ordinal);

    public bool TryConsume(string clientKey, DateTimeOffset now, out int retryAfterSeconds)
    {
        if (_windows.Count > MaximumClientWindows)
            PurgeExpired(now);

        var state = _windows.GetOrAdd(clientKey, _ => new WindowState(now));
        lock (state.Gate)
        {
            if (now - state.Start >= Window)
            {
                state.Start = now;
                state.Count = 0;
            }

            if (state.Count >= MaximumRequestsPerMinute)
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((Window - (now - state.Start)).TotalSeconds));
                return false;
            }

            state.Count++;
            retryAfterSeconds = 0;
            return true;
        }
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var pair in _windows)
        {
            if (now - pair.Value.Start >= Window && Monitor.TryEnter(pair.Value.Gate))
            {
                try
                {
                    if (now - pair.Value.Start >= Window)
                        _windows.TryRemove(pair);
                }
                finally
                {
                    Monitor.Exit(pair.Value.Gate);
                }
            }
        }
    }

    private sealed class WindowState
    {
        public WindowState(DateTimeOffset start) => Start = start;
        public object Gate { get; } = new();
        public DateTimeOffset Start { get; set; }
        public int Count { get; set; }
    }
}
