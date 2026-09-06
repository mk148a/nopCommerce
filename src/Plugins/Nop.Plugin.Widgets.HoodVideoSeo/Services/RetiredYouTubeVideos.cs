namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

/// <summary>
/// Contains only video IDs confirmed unavailable from their public YouTube watch pages.
/// Retired videos must not create embeds, watch routes, or sitemap entries.
/// </summary>
public static class RetiredYouTubeVideos
{
    private static readonly HashSet<string> Ids = new(StringComparer.OrdinalIgnoreCase)
    {
        "B7H4igrP0Wo",
        "FxSceVSLx4U",
        "tPxPcvSjLJk"
    };

    public static bool Contains(string youtubeId)
    {
        return !string.IsNullOrWhiteSpace(youtubeId) && Ids.Contains(youtubeId);
    }

    /// <summary>
    /// Applies the reviewed local deny-list before asking the availability service.
    /// This keeps alternate Razor/partial rendering paths from queuing a YouTube
    /// request for a video that is already known to be retired.
    /// </summary>
    public static bool ShouldRender(string youtubeId, IYouTubeVideoAvailabilityService availabilityService)
    {
        ArgumentNullException.ThrowIfNull(availabilityService);

        return !Contains(youtubeId) && availabilityService.GetAvailability(youtubeId) != false;
    }
}
