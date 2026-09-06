using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

/// <summary>
/// Reads a deployment-local, reviewed date registry.  This service never calls YouTube at
/// request time: updates are an explicit maintenance operation against public watch pages.
/// </summary>
public sealed class YouTubePublicationDateService : IYouTubePublicationDateService
{
    private const string CacheKey = "hood.video-seo.publication-dates.v1";
    private static readonly Regex YouTubeIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    private readonly IMemoryCache _memoryCache;
    private readonly string _registryPath = Path.Combine(
        AppContext.BaseDirectory, "Plugins", "Widgets.HoodVideoSeo", "Content", "youtube-publication-dates.json");

    public YouTubePublicationDateService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public DateOnly? GetPublicationDate(string youtubeId)
    {
        if (string.IsNullOrWhiteSpace(youtubeId) || !YouTubeIdPattern.IsMatch(youtubeId))
            return null;

        var dates = _memoryCache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return LoadRegistry();
        }) ?? new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);

        return dates.TryGetValue(youtubeId, out var date) ? date : null;
    }

    private Dictionary<string, DateOnly> LoadRegistry()
    {
        try
        {
            if (!File.Exists(_registryPath))
                return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);

            var rawDates = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_registryPath))
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dates = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
            foreach (var (videoId, dateValue) in rawDates)
            {
                if (YouTubeIdPattern.IsMatch(videoId) &&
                    DateOnly.TryParseExact(dateValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsed))
                    dates[videoId] = parsed;
            }

            return dates;
        }
        catch (IOException)
        {
            return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
