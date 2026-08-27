namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

public interface IYouTubeVideoAvailabilityService
{
    /// <summary>
    /// Returns only a local cached result. A cache miss returns unknown (null)
    /// and never schedules network work.
    /// </summary>
    bool? GetCachedAvailability(string youtubeId);

    /// <summary>
    /// Returns the cached result without waiting for YouTube. A cache miss queues one
    /// bounded background refresh and returns unknown (null), preserving the video.
    /// </summary>
    bool? GetAvailability(string youtubeId);
}
