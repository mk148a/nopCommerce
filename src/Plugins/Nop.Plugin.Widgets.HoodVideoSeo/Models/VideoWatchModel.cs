namespace Nop.Plugin.Widgets.HoodVideoSeo.Models;

public sealed record VideoWatchModel(
    string Title,
    string Description,
    string EmbedUrl,
    string YouTubeUrl,
    string ProductName,
    string ProductUrl,
    string CanonicalUrl,
    string ThumbnailUrl,
    DateOnly? PublicationDate);
