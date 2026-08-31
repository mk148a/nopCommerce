using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;

/// <summary>
/// Replaces legacy YouTube iframes embedded in product descriptions with light, crawlable watch-page cards.
/// The database content is deliberately left unchanged.
/// </summary>
public sealed class YouTubeEmbedResultFilter : IAsyncResultFilter
{
    private static readonly Regex YouTubeIframe = new(
        "<iframe\\b[^>]*\\bsrc\\s*=\\s*[\"'](?<url>[^\"']+)[\"'][^>]*>.*?<\\/iframe>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly IWorkContext _workContext;
    private readonly IYouTubeVideoAvailabilityService _videoAvailabilityService;

    public YouTubeEmbedResultFilter(
        IWorkContext workContext,
        IYouTubeVideoAvailabilityService videoAvailabilityService)
    {
        _workContext = workContext;
        _videoAvailabilityService = videoAvailabilityService;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult { Model: ProductDetailsModel product })
        {
            // ProductVideo is rendered independently from the description. Remove
            // reviewed retired IDs from that gallery before it reaches the view.
            product.VideoModels = product.VideoModels
                .Where(item => !YouTubeVideoUrlParser.TryParseEmbedUrl(item.VideoUrl, out var id) || !RetiredYouTubeVideos.Contains(id))
                .ToList();

            if (string.IsNullOrWhiteSpace(product.FullDescription) ||
                YouTubeVideoUrlParser.ExtractEmbedIds(product.FullDescription).Count == 0)
            {
                await next();
                return;
            }

            var languageCode = (await _workContext.GetWorkingLanguageAsync()).UniqueSeoCode;
            var mappedVideoIds = product.VideoModels
                .Select(item => YouTubeVideoUrlParser.TryParseEmbedUrl(item.VideoUrl, out var id) ? id : null)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidateVideoIds = YouTubeVideoUrlParser.ExtractEmbedIds(product.FullDescription)
                .Concat(mappedVideoIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var unavailableVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var videoId in candidateVideoIds)
            {
                if (!RetiredYouTubeVideos.ShouldRender(videoId, _videoAvailabilityService))
                    unavailableVideoIds.Add(videoId);
            }
            var renderedDescriptionVideoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            product.FullDescription = YouTubeIframe.Replace(product.FullDescription, match =>
            {
                if (!YouTubeVideoUrlParser.TryParseEmbedUrl(match.Groups["url"].Value, out var youtubeId))
                    return match.Value;

                // ProductVideo renders the canonical gallery. Do not repeat a video
                // when a historical description iframe points to the same YouTube ID.
                if (unavailableVideoIds.Contains(youtubeId) || mappedVideoIds.Contains(youtubeId) || !renderedDescriptionVideoIds.Add(youtubeId))
                    return string.Empty;

                var title = $"{product.Name} video";
                return $"<a class=\"hood-product-video-card hood-description-video-card\" href=\"/{languageCode}/watch/{product.Id}/{youtubeId}\" data-youtube-id=\"{youtubeId}\" aria-label=\"{System.Net.WebUtility.HtmlEncode(title)}\"><img src=\"https://i.ytimg.com/vi/{youtubeId}/hqdefault.jpg\" alt=\"{System.Net.WebUtility.HtmlEncode(title)}\" loading=\"lazy\" width=\"480\" height=\"360\" /><span class=\"hood-product-video-play\" aria-hidden=\"true\">&#9654;</span><span class=\"hood-product-video-label\">Watch video</span></a>";
            });

            product.FullDescription += "<style>.hood-description-video-card{margin:16px 0;max-width:720px}.hood-product-video-card{aspect-ratio:16/9;background:#111;color:#fff;display:block;overflow:hidden;position:relative;text-decoration:none;width:100%}.hood-product-video-card img{display:block;height:100%;object-fit:cover;width:100%}.hood-product-video-play{align-items:center;background:rgba(0,0,0,.72);border-radius:50%;display:flex;font-size:28px;height:60px;justify-content:center;left:50%;position:absolute;top:50%;transform:translate(-50%,-50%);width:60px}.hood-product-video-label{background:rgba(0,0,0,.76);bottom:0;left:0;padding:8px 10px;position:absolute;right:0}.hood-video-modal-open{overflow:hidden}.hood-video-modal{align-items:center;background:rgba(0,0,0,.84);bottom:0;display:flex;justify-content:center;left:0;padding:16px;position:fixed;right:0;top:0;z-index:2147483647}.hood-video-modal__dialog{aspect-ratio:16/9;background:#000;max-width:1100px;position:relative;width:min(100%,1100px)}.hood-video-modal__dialog iframe{border:0;height:100%;width:100%}.hood-video-modal__close{background:#fff;border:0;border-radius:50%;color:#111;cursor:pointer;font-size:28px;height:40px;line-height:36px;position:absolute;right:-8px;top:-8px;width:40px;z-index:1}</style><script defer src=\"/Plugins/Widgets.HoodVideoSeo/Content/hood-video-player.js?v=20260831-noautoplay\"></script>";
        }

        await next();
    }
}
