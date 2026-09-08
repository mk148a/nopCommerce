using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Controllers;

public sealed partial class VideoWatchController
{
    private const int SitemapTitleMaxLength = 100;
    private const int SitemapDescriptionMaxLength = 2048;

    /// <summary>
    /// Lists the real product-video watch pages for crawler discovery.
    /// Publication dates come only from the reviewed public-YouTube registry; an
    /// unknown date is omitted rather than inferred from product data.
    /// </summary>
    [HttpGet]
    [CheckAccessClosedStore(ignore: true)]
    [CheckAccessPublicStore(ignore: true)]
    [CheckLanguageSeoCode(ignore: true)]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ProductVideos()
    {
        var languages = await _languageService.GetAllLanguagesAsync();
        var language = languages.FirstOrDefault(item => item.Published &&
                           string.Equals(item.UniqueSeoCode, "en", StringComparison.OrdinalIgnoreCase))
                       ?? languages.FirstOrDefault(item => item.Published);
        if (language is null || string.IsNullOrWhiteSpace(language.UniqueSeoCode))
            return Content(BuildSitemap(Array.Empty<VideoSitemapEntry>()), "application/xml", Encoding.UTF8);

        var productsWithEmbeddedVideos = await _productRepository.Table
            .Where(product => product.Published && !product.Deleted && product.VisibleIndividually &&
                (product.FullDescription.Contains("youtube.com/embed/") ||
                 product.FullDescription.Contains("youtube-nocookie.com/embed/") ||
                 product.ShortDescription.Contains("youtube.com/embed/") ||
                 product.ShortDescription.Contains("youtube-nocookie.com/embed/")))
            .Select(product => new { product.Id, product.Name, product.FullDescription, product.ShortDescription })
            .ToListAsync();

        var mappedVideos = await (from mapping in _productVideoRepository.Table
                                  join video in _videoRepository.Table on mapping.VideoId equals video.Id
                                  join product in _productRepository.Table on mapping.ProductId equals product.Id
                                  where product.Published && !product.Deleted && product.VisibleIndividually
                                  select new { ProductId = product.Id, product.Name, product.FullDescription, product.ShortDescription, video.VideoUrl }).ToListAsync();

        var videoIdsByProduct = new Dictionary<int, HashSet<string>>();
        var productMetadata = new Dictionary<int, ProductVideoMetadata>();
        foreach (var product in productsWithEmbeddedVideos)
        {
            AddVideoIds(videoIdsByProduct, product.Id, product.FullDescription);
            AddVideoIds(videoIdsByProduct, product.Id, product.ShortDescription);
            productMetadata[product.Id] = new ProductVideoMetadata(product.Name, product.ShortDescription, product.FullDescription);
        }

        foreach (var video in mappedVideos)
        {
            AddVideoIds(videoIdsByProduct, video.ProductId, video.VideoUrl);
            productMetadata.TryAdd(video.ProductId, new ProductVideoMetadata(video.Name, video.ShortDescription, video.FullDescription));
        }

        var storeUrl = _webHelper.GetStoreLocation().TrimEnd('/');
        var entries = new List<VideoSitemapEntry>();
        foreach (var pair in videoIdsByProduct.OrderBy(item => item.Key))
        {
            var sequence = 0;
            foreach (var videoId in pair.Value.OrderBy(item => item, StringComparer.Ordinal))
            {
                // A reviewed retired ID must stay absent even on a cold cache.
                // This check is local and never performs a YouTube request.
                if (RetiredYouTubeVideos.Contains(videoId))
                    continue;

                // Do not advertise a watch URL for a video YouTube has conclusively
                // removed or refused to embed. Unknown means a transient check failure.
                // Sitemap generation is cache-only. A cold sitemap may contain an
                // unknown video, but it must never fan out into one YouTube request
                // per catalog video.
                if (_videoAvailabilityService.GetCachedAvailability(videoId) == false)
                    continue;

                sequence++;
                entries.Add(BuildEntry(pair.Key, videoId, sequence, productMetadata[pair.Key], storeUrl, language.UniqueSeoCode,
                    _publicationDateService.GetPublicationDate(videoId)));
            }
        }

        entries = entries.OrderBy(entry => entry.WatchUrl, StringComparer.Ordinal).ToList();

        return Content(BuildSitemap(entries), "application/xml", Encoding.UTF8);
    }

    private static void AddVideoIds(IDictionary<int, HashSet<string>> entries, int productId, string source)
    {
        if (productId <= 0 || string.IsNullOrWhiteSpace(source))
            return;

        foreach (var youtubeId in YouTubeVideoUrlParser.ExtractEmbedIds(source))
        {
            if (!entries.TryGetValue(productId, out var videoIds))
                entries[productId] = videoIds = new HashSet<string>(StringComparer.Ordinal);

            videoIds.Add(youtubeId);
        }
    }

    private static VideoSitemapEntry BuildEntry(int productId, string videoId, int sequence, ProductVideoMetadata product, string storeUrl, string languageCode, DateOnly? publicationDate)
    {
        var name = ToPlainText(product.Name);
        var description = ToPlainText(string.IsNullOrWhiteSpace(product.ShortDescription) ? product.FullDescription : product.ShortDescription);
        if (string.IsNullOrWhiteSpace(description))
            description = $"Product video for {name}.";

        var title = Truncate($"{name} video {sequence}", SitemapTitleMaxLength);
        description = Truncate(description, SitemapDescriptionMaxLength);

        return new VideoSitemapEntry(
            WatchUrl: $"{storeUrl}/{languageCode}/watch/{productId}/{videoId}",
            ThumbnailUrl: $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg",
            Title: title,
            Description: description,
            PlayerUrl: $"https://www.youtube-nocookie.com/embed/{videoId}",
            PublicationDate: publicationDate);
    }

    private static string ToPlainText(string value)
    {
        var text = Regex.Replace(System.Net.WebUtility.HtmlDecode(value ?? string.Empty), "<[^>]+>", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        var result = value[..maxLength];
        if (char.IsHighSurrogate(result[^1]))
            result = result[..^1];

        return result.TrimEnd();
    }

    private static string BuildSitemap(IEnumerable<VideoSitemapEntry> entries)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, OmitXmlDeclaration = false };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            writer.WriteAttributeString("xmlns", "video", null, "http://www.google.com/schemas/sitemap-video/1.1");
            foreach (var entry in entries)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", entry.WatchUrl);
                writer.WriteStartElement("video", "video", "http://www.google.com/schemas/sitemap-video/1.1");
                writer.WriteElementString("thumbnail_loc", "http://www.google.com/schemas/sitemap-video/1.1", entry.ThumbnailUrl);
                writer.WriteElementString("title", "http://www.google.com/schemas/sitemap-video/1.1", entry.Title);
                writer.WriteElementString("description", "http://www.google.com/schemas/sitemap-video/1.1", entry.Description);
                writer.WriteStartElement("player_loc", "http://www.google.com/schemas/sitemap-video/1.1");
                writer.WriteAttributeString("allow_embed", "yes");
                writer.WriteString(entry.PlayerUrl);
                writer.WriteEndElement();
                if (entry.PublicationDate is not null)
                    writer.WriteElementString("publication_date", "http://www.google.com/schemas/sitemap-video/1.1",
                        entry.PublicationDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed record ProductVideoMetadata(string Name, string ShortDescription, string FullDescription);

    private sealed record VideoSitemapEntry(string WatchUrl, string ThumbnailUrl, string Title, string Description, string PlayerUrl, DateOnly? PublicationDate);
}
