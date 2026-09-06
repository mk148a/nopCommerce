using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

/// <summary>
/// Parses only canonical HTTPS YouTube embed URLs. Database URLs are never
/// trusted as player URLs; callers use the validated ID to build output.
/// </summary>
public static class YouTubeVideoUrlParser
{
    private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    private static readonly Regex AbsoluteUrlCandidatePattern = new(
        "https://[^\\s\\\"'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ApprovedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "youtube-nocookie.com",
        "www.youtube-nocookie.com"
    };

    public static bool IsValidVideoId(string youtubeId) =>
        !string.IsNullOrWhiteSpace(youtubeId) && VideoIdPattern.IsMatch(youtubeId);

    public static bool TryParseEmbedUrl(string source, out string youtubeId)
    {
        youtubeId = string.Empty;
        if (string.IsNullOrWhiteSpace(source))
            return false;

        var decoded = WebUtility.HtmlDecode(source.Trim());
        if (!Uri.TryCreate(decoded, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !ApprovedHosts.Contains(uri.IdnHost))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 ||
            !string.Equals(segments[0], "embed", StringComparison.OrdinalIgnoreCase) ||
            !IsValidVideoId(segments[1]))
        {
            return false;
        }

        youtubeId = segments[1];
        return true;
    }

    public static IReadOnlyCollection<string> ExtractEmbedIds(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return Array.Empty<string>();

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AbsoluteUrlCandidatePattern.Matches(WebUtility.HtmlDecode(source)))
        {
            if (TryParseEmbedUrl(match.Value, out var youtubeId))
                ids.Add(youtubeId);
        }

        return ids;
    }
}
