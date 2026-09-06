using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Data;
using Nop.Plugin.Widgets.HoodVideoSeo.Models;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using Nop.Web.Controllers;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Controllers;

/// <summary>
/// Keeps product pages commerce-focused while exposing a real watch page for each YouTube helper video.
/// </summary>
public sealed partial class VideoWatchController : BasePublicController
{
    private readonly ILanguageService _languageService;
    private readonly ILocalizationService _localizationService;
    private readonly IProductService _productService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IVideoService _videoService;
    private readonly IRepository<ProductVideo> _productVideoRepository;
    private readonly IRepository<Video> _videoRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IWebHelper _webHelper;
    private readonly IYouTubeVideoAvailabilityService _videoAvailabilityService;
    private readonly IYouTubePublicationDateService _publicationDateService;

    public VideoWatchController(
        ILanguageService languageService,
        ILocalizationService localizationService,
        IProductService productService,
        IUrlRecordService urlRecordService,
        IVideoService videoService,
        IRepository<ProductVideo> productVideoRepository,
        IRepository<Video> videoRepository,
        IRepository<Product> productRepository,
        IWebHelper webHelper,
        IYouTubeVideoAvailabilityService videoAvailabilityService,
        IYouTubePublicationDateService publicationDateService)
    {
        _languageService = languageService;
        _localizationService = localizationService;
        _productService = productService;
        _urlRecordService = urlRecordService;
        _videoService = videoService;
        _productVideoRepository = productVideoRepository;
        _videoRepository = videoRepository;
        _productRepository = productRepository;
        _webHelper = webHelper;
        _videoAvailabilityService = videoAvailabilityService;
        _publicationDateService = publicationDateService;
    }

    public async Task<IActionResult> Watch(string language, int productId, string youtubeId)
    {
        if (!YouTubeVideoUrlParser.IsValidVideoId(youtubeId))
            return InvokeHttp404();

        // Retired IDs are an explicit, reviewed unavailable set. Fail locally
        // before loading product data or queuing an availability HTTP request.
        if (RetiredYouTubeVideos.Contains(youtubeId))
            return InvokeHttp404();

        var targetLanguage = (await _languageService.GetAllLanguagesAsync())
            .FirstOrDefault(item => item.Published &&
                string.Equals(item.UniqueSeoCode, language, StringComparison.OrdinalIgnoreCase));
        if (targetLanguage is null)
            return InvokeHttp404();

        var product = await _productService.GetProductByIdAsync(productId);
        if (product is null || product.Deleted || !product.Published || !product.VisibleIndividually)
            return InvokeHttp404();

        var productVideos = await _videoService.GetVideosByProductIdAsync(product.Id);
        var mappedVideo = productVideos
            .FirstOrDefault(item => YouTubeVideoUrlParser.TryParseEmbedUrl(item.VideoUrl, out var id) &&
                string.Equals(id, youtubeId, StringComparison.OrdinalIgnoreCase));
        var embeddedInDescription = YouTubeVideoUrlParser.ExtractEmbedIds(product.FullDescription).Contains(youtubeId, StringComparer.OrdinalIgnoreCase);
        var embeddedInShortDescription = YouTubeVideoUrlParser.ExtractEmbedIds(product.ShortDescription).Contains(youtubeId, StringComparer.OrdinalIgnoreCase);
        if (mappedVideo is null && !embeddedInDescription && !embeddedInShortDescription)
            return InvokeHttp404();

        // Validate the published product relationship before queuing any external
        // availability lookup. Random 11-character watch IDs therefore stay local.
        if (_videoAvailabilityService.GetAvailability(youtubeId) == false)
            return InvokeHttp404();

        var videoOrdinal = GetVideoOrdinal(product, productVideos, youtubeId);

        var productName = await _localizationService.GetLocalizedAsync(product, item => item.Name, targetLanguage.Id);
        var productDescription = await _localizationService.GetLocalizedAsync(product, item => item.ShortDescription, targetLanguage.Id);
        if (string.IsNullOrWhiteSpace(productDescription))
            productDescription = await _localizationService.GetLocalizedAsync(product, item => item.FullDescription, targetLanguage.Id);

        productDescription = Regex.Replace(WebUtility.HtmlDecode(productDescription ?? string.Empty), "<[^>]+>", " ").Trim();
        if (productDescription.Length > 300)
            productDescription = productDescription[..300].TrimEnd() + "…";

        var videoTitle = $"{productName} video {videoOrdinal}";
        var videoDescription = string.IsNullOrWhiteSpace(productDescription)
            ? $"Watch video {videoOrdinal} for {productName}."
            : $"Watch video {videoOrdinal} for {productName}. {productDescription}";

        var productSlug = await _urlRecordService.GetSeNameAsync(product, targetLanguage.Id);
        if (string.IsNullOrWhiteSpace(productSlug))
            return InvokeHttp404();

        var routePath = $"/{targetLanguage.UniqueSeoCode}/watch/{product.Id}/{youtubeId}";
        var model = new VideoWatchModel(
            Title: videoTitle,
            Description: videoDescription,
            EmbedUrl: BuildEmbedUrl(youtubeId, _webHelper.GetStoreLocation()),
            YouTubeUrl: $"https://www.youtube.com/watch?v={youtubeId}",
            ProductName: productName,
            ProductUrl: $"/{targetLanguage.UniqueSeoCode}/{productSlug}",
            CanonicalUrl: $"{_webHelper.GetStoreLocation().TrimEnd('/')}{routePath}",
            ThumbnailUrl: $"https://i.ytimg.com/vi/{youtubeId}/hqdefault.jpg",
            PublicationDate: _publicationDateService.GetPublicationDate(youtubeId));

        return View("~/Plugins/Widgets.HoodVideoSeo/Views/VideoWatch/Watch.cshtml", model);
    }

    private static string BuildEmbedUrl(string youtubeId, string storeLocation)
    {
        if (!YouTubeVideoUrlParser.IsValidVideoId(youtubeId))
            throw new ArgumentException("A valid YouTube video ID is required.", nameof(youtubeId));

        // Never propagate a database VideoUrl into HTML or structured data. The
        // validated ID is the only input used to construct the player URL.
        var origin = Uri.EscapeDataString(storeLocation.TrimEnd('/'));
        return $"https://www.youtube-nocookie.com/embed/{youtubeId}?enablejsapi=1&playsinline=1&origin={origin}&widget_referrer={origin}";
    }

    private static int GetVideoOrdinal(Product product, IEnumerable<Video> productVideos, string youtubeId)
    {
        var videoIds = new List<string>();
        videoIds.AddRange(YouTubeVideoUrlParser.ExtractEmbedIds(product.FullDescription));
        videoIds.AddRange(YouTubeVideoUrlParser.ExtractEmbedIds(product.ShortDescription));
        videoIds.AddRange(productVideos
            .Select(video => YouTubeVideoUrlParser.TryParseEmbedUrl(video.VideoUrl, out var id) ? id : null)
            .OfType<string>());

        var ordinal = videoIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .FindIndex(id => string.Equals(id, youtubeId, StringComparison.OrdinalIgnoreCase));

        return ordinal >= 0 ? ordinal + 1 : 1;
    }
}
