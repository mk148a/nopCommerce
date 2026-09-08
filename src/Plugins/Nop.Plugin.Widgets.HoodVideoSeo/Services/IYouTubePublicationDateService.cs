namespace Nop.Plugin.Widgets.HoodVideoSeo.Services;

/// <summary>
/// Provides only publication dates explicitly verified from public YouTube watch pages.
/// Missing or invalid data intentionally produces no date markup.
/// </summary>
public interface IYouTubePublicationDateService
{
    DateOnly? GetPublicationDate(string youtubeId);
}
