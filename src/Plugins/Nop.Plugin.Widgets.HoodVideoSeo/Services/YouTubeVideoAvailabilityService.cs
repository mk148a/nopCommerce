using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

/// <summary>
/// Uses YouTube's public oEmbed response to suppress conclusively unavailable or
/// non-embeddable legacy videos. Request paths read only the local cache; a miss
/// schedules one short background refresh and fails open.
/// </summary>
public sealed class YouTubeVideoAvailabilityService : IYouTubeVideoAvailabilityService
{
    public const string HttpClientName = "HoodVideoSeo.YouTubeAvailability";
    public const int MaxConcurrentRefreshes = 4;
    public const int MaxPendingRefreshes = 64;
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(3);

    private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    private static readonly TimeSpan ConclusiveCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan UnknownCacheDuration = TimeSpan.FromMinutes(5);
    private const int MaximumOEmbedBytes = 64 * 1024;

    private readonly ConcurrentDictionary<string, byte> _refreshes = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _refreshSlots = new(MaxConcurrentRefreshes, MaxConcurrentRefreshes);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeVideoAvailabilityService> _logger;
    private readonly IMemoryCache _memoryCache;
    private int _pendingRefreshCount;

    public YouTubeVideoAvailabilityService(
        IHttpClientFactory httpClientFactory,
        ILogger<YouTubeVideoAvailabilityService> logger,
        IMemoryCache memoryCache)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public bool? GetAvailability(string youtubeId)
    {
        var cached = GetCachedAvailability(youtubeId);
        if (cached is not null || !VideoIdPattern.IsMatch(youtubeId ?? string.Empty))
            return cached;

        var cacheKey = $"hood.video-seo.availability.{youtubeId}";
        QueueRefresh(youtubeId, cacheKey);
        return null;
    }

    public bool? GetCachedAvailability(string youtubeId)
    {
        if (!VideoIdPattern.IsMatch(youtubeId ?? string.Empty))
            return false;

        var cacheKey = $"hood.video-seo.availability.{youtubeId}";
        return _memoryCache.TryGetValue<AvailabilityCacheEntry>(cacheKey, out var cached)
            ? cached?.Value
            : null;
    }

    private void QueueRefresh(string youtubeId, string cacheKey)
    {
        if (!_refreshes.TryAdd(youtubeId, 0))
            return;

        if (Interlocked.Increment(ref _pendingRefreshCount) > MaxPendingRefreshes)
        {
            Interlocked.Decrement(ref _pendingRefreshCount);
            _refreshes.TryRemove(youtubeId, out _);
            _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
            return;
        }

        _ = Task.Run(async () =>
        {
            var slotEntered = false;
            try
            {
                await _refreshSlots.WaitAsync();
                slotEntered = true;
                await RefreshAsync(youtubeId, cacheKey);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
                _logger.LogDebug(exception, "YouTube availability refresh was inconclusive for {YouTubeId}", youtubeId);
            }
            catch (Exception exception)
            {
                _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
                _logger.LogWarning(exception, "YouTube availability refresh failed for {YouTubeId}", youtubeId);
            }
            finally
            {
                if (slotEntered)
                    _refreshSlots.Release();
                Interlocked.Decrement(ref _pendingRefreshCount);
                _refreshes.TryRemove(youtubeId, out _);
            }
        });
    }

    private async Task RefreshAsync(string youtubeId, string cacheKey)
    {
        var videoUrl = Uri.EscapeDataString($"https://www.youtube.com/watch?v={youtubeId}");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"oembed?url={videoUrl}&format=json");
        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var contentType = response.Content.Headers.ContentType;
            var contentLength = response.Content.Headers.ContentLength;
            if (!IsJsonContentType(contentType) || contentLength > MaximumOEmbedBytes)
            {
                _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
                return;
            }

            try
            {
                var payload = await ReadBoundedContentAsync(response.Content, MaximumOEmbedBytes);
                if (payload is null)
                {
                    _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
                    return;
                }

                using var document = JsonDocument.Parse(payload, new JsonDocumentOptions
                {
                    MaxDepth = 16,
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
                var root = document.RootElement;
                var typeIsVideo = root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                    string.Equals(type.GetString(), "video", StringComparison.OrdinalIgnoreCase);
                var providerIsYouTube = root.TryGetProperty("provider_name", out var provider) && provider.ValueKind == JsonValueKind.String &&
                    string.Equals(provider.GetString(), "YouTube", StringComparison.OrdinalIgnoreCase);
                var hasMatchingEmbed = root.TryGetProperty("html", out var html) && html.ValueKind == JsonValueKind.String &&
                    YouTubeVideoUrlParser.ExtractEmbedIds(html.GetString()).Contains(youtubeId, StringComparer.OrdinalIgnoreCase);

                _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(typeIsVideo && providerIsYouTube && hasMatchingEmbed ? true : null),
                    typeIsVideo && providerIsYouTube && hasMatchingEmbed ? ConclusiveCacheDuration : UnknownCacheDuration);
            }
            catch (JsonException exception)
            {
                _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
                _logger.LogDebug(exception, "YouTube oEmbed JSON was invalid for {YouTubeId}", youtubeId);
            }
            return;
        }

        // 403 (embedding/access denied) and 404 (removed) are cached briefly so
        // transient policy changes do not suppress a legitimate video for a day.
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(false), NegativeCacheDuration);
            return;
        }

        // Avoid retrying YouTube for every request during rate limits or outages.
        _memoryCache.Set(cacheKey, new AvailabilityCacheEntry(null), UnknownCacheDuration);
    }

    private static bool IsJsonContentType(MediaTypeHeaderValue contentType)
    {
        var mediaType = contentType?.MediaType;
        return !string.IsNullOrWhiteSpace(mediaType) &&
            (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
             mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<byte[]> ReadBoundedContentAsync(HttpContent content, int maximumBytes)
    {
        await using var source = await content.ReadAsStreamAsync();
        using var destination = new MemoryStream(Math.Min(maximumBytes, 8192));
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer);
            if (read == 0)
                return destination.ToArray();
            if (destination.Length + read > maximumBytes)
                return null;
            await destination.WriteAsync(buffer.AsMemory(0, read));
        }
    }

    private sealed record AvailabilityCacheEntry(bool? Value);
}
