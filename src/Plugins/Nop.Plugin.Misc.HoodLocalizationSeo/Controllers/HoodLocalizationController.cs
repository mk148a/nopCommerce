using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Core.Rss;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Models.Common;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Controllers;

public sealed class HoodLocalizationController : BasePublicController
{
    private const string SitemapFallbackPath = "/sitemap.xml";
    private const string SitemapXmlNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    // The protocol's "50 MB" limit is exactly 50 MiB (52,428,800 bytes).
    // See https://www.sitemaps.org/protocol.html#usingSitemapIndex
    private const long SitemapProtocolMaxBytes = 50L * 1024 * 1024;
    private const int SitemapProtocolMaxEntries = 50_000;
    private static readonly Regex SitemapFileNamePattern = new(
        @"^sitemap-\d+-\d+-\d+\.xml$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PublicSitemapFileNamePattern = new(
        @"^sitemap-(?<id>[1-9]\d*)\.xml$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly BlogSettings _blogSettings;
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly ICommonModelFactory _commonModelFactory;
    private readonly ILanguageService _languageService;
    private readonly ILocalizationService _localizationService;
    private readonly INopFileProvider _nopFileProvider;
    private readonly ISitemapHreflangStreamTransformer _sitemapHreflangStreamTransformer;
    private readonly ISitemapModelFactory _sitemapModelFactory;
    private readonly SitemapXmlSettings _sitemapXmlSettings;
    private readonly IStoreContext _storeContext;
    private readonly ITopicService _topicService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IWebHelper _webHelper;
    private readonly IWorkContext _workContext;

    public HoodLocalizationController(BlogSettings blogSettings,
        SitemapXmlSettings sitemapXmlSettings,
        IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        ICommonModelFactory commonModelFactory,
        ILanguageService languageService,
        ILocalizationService localizationService,
        INopFileProvider nopFileProvider,
        ISitemapHreflangStreamTransformer sitemapHreflangStreamTransformer,
        ISitemapModelFactory sitemapModelFactory,
        IStoreContext storeContext,
        ITopicService topicService,
        IUrlRecordService urlRecordService,
        IWebHelper webHelper,
        IWorkContext workContext)
    {
        _blogSettings = blogSettings;
        _sitemapXmlSettings = sitemapXmlSettings;
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _commonModelFactory = commonModelFactory;
        _languageService = languageService;
        _localizationService = localizationService;
        _nopFileProvider = nopFileProvider;
        _sitemapHreflangStreamTransformer = sitemapHreflangStreamTransformer;
        _sitemapModelFactory = sitemapModelFactory;
        _storeContext = storeContext;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    // The plugin owns both root and numbered sitemap routes. Core still builds
    // and caches the source artifact; this action validates it and rewrites
    // hreflang only in a bounded delete-on-close response stream.
    [HttpGet]
    [CheckAccessClosedStore(ignore: true)]
    [CheckAccessPublicStore(ignore: true)]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> SitemapXml(int id = 0)
    {
        if (!_sitemapXmlSettings.SitemapXmlEnabled)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (id < 0)
            return NotFound();

        var store = await _storeContext.GetCurrentStoreAsync();
        var language = await _workContext.GetWorkingLanguageAsync();
        var expectedPath = GetExpectedSitemapPath(store.Id, language.Id, id);
        var rootPath = GetExpectedSitemapPath(store.Id, language.Id, 0);
        Uri canonicalRoot = null;
        var hasCanonicalOrigin = SitemapCanonicalOrigin.TryCreate(store.Url,
            _webHelper.GetStoreLocation(), out var canonicalOrigin);
        var hasCanonicalRoot = hasCanonicalOrigin && Uri.TryCreate(canonicalOrigin,
            SitemapFallbackPath.TrimStart('/'), out canonicalRoot);

        // Never serve a cached/LKG artifact or synthesize an alias when the
        // configured store URL cannot establish a trusted HTTP(S) origin.
        // This guard must run before opening id=0 (including the id=1 alias
        // path), otherwise an invalid Store.Url could still produce a 200.
        if (!hasCanonicalRoot)
            return RetryableServiceUnavailable();

        if (id == 0)
            return await ServeCanonicalRootAsync(store, rootPath, canonicalRoot);

        var rootArtifact = await TryOpenValidatedSitemapAsync(rootPath, rootPath,
            canonicalRoot);

        if (rootArtifact is null)
        {
            // Every cold numbered request first participates in the one core
            // id=0 lock. This prevents independent id=1..N generation storms
            // and establishes the canonical root before part routing.
            try
            {
                var rootModel = await _sitemapModelFactory.PrepareSitemapXmlModelAsync(0);
                rootArtifact = await TryOpenValidatedSitemapAsync(rootModel?.SitemapXmlPath,
                    rootPath, canonicalRoot);
            }
            catch (InvalidOperationException)
            {
                // Another request owns the canonical generation lock. A
                // complete, protocol-valid old part remains a safe LKG response.
                return await TryCreateValidatedPartResultAsync(expectedPath, store, canonicalRoot)
                    ?? RetryableServiceUnavailable();
            }

            if (rootArtifact is null)
            {
                return await TryCreateValidatedPartResultAsync(expectedPath, store, canonicalRoot)
                    ?? RetryableServiceUnavailable();
            }
        }

        try
        {
            if (rootArtifact.Catalog.Kind == SitemapCatalogKind.UrlSet)
            {
                if (id == 1)
                {
                    // In a one-part topology sitemap-1.xml is the canonical
                    // root itself. Reuse this exact validated handle instead of
                    // generating and storing a duplicate multi-megabyte file.
                    return await CreateValidatedSitemapResultAsync(rootArtifact, store);
                }

                // Search Console does not follow redirects for submitted
                // sitemap URLs. Return a valid 200 index alias which points to
                // the canonical urlset using only trusted Store.Url data.
                return hasCanonicalRoot
                    ? CreateSitemapIndexAlias([canonicalRoot])
                    : RetryableServiceUnavailable();
            }

            if (rootArtifact.Catalog.Kind == SitemapCatalogKind.SitemapIndex &&
                !rootArtifact.Catalog.PartIds.Contains(id))
            {
                // A submitted stale URL can itself be a sitemap index. Serving
                // the exact validated canonical index here does not create a
                // nested index and preserves all current part metadata.
                return File(rootArtifact.DetachStream(), MimeTypes.ApplicationXml);
            }
        }
        finally
        {
            rootArtifact.Dispose();
        }

        try
        {
            var model = await _sitemapModelFactory.PrepareSitemapXmlModelAsync(id);
            var partArtifact = await TryOpenValidatedSitemapAsync(model?.SitemapXmlPath,
                expectedPath, canonicalRoot, SitemapCatalogKind.UrlSet);
            if (partArtifact is not null)
            {
                try
                {
                    return await CreateValidatedSitemapResultAsync(partArtifact, store);
                }
                finally
                {
                    partArtifact.Dispose();
                }
            }

            // With an unknown root, or for a listed/current part, do not claim
            // success when the factory did not produce a safe complete file.
            return RetryableServiceUnavailable();
        }
        catch (InvalidOperationException)
        {
            // Core uses InvalidOperationException when another request owns the
            // sitemap-generation lock. Serve the last complete file when one is
            // present; otherwise advertise a retry instead of masking the lock
            // as a permanent redirect or a 500.
            return await TryCreateValidatedPartResultAsync(expectedPath, store, canonicalRoot)
                ?? RetryableServiceUnavailable();
        }
    }

    private async Task<IActionResult> ServeCanonicalRootAsync(Store store,
        string rootPath,
        Uri canonicalRoot)
    {
        ValidatedSitemapArtifact rootArtifact;
        try
        {
            var model = await _sitemapModelFactory.PrepareSitemapXmlModelAsync(0);
            rootArtifact = await TryOpenValidatedSitemapAsync(model?.SitemapXmlPath,
                rootPath, canonicalRoot);
        }
        catch (InvalidOperationException)
        {
            // A concurrent core build may hold the lock. Serve only a complete
            // protocol-valid LKG artifact; otherwise make the retry explicit.
            rootArtifact = await TryOpenValidatedSitemapAsync(rootPath, rootPath,
                canonicalRoot);
        }

        if (rootArtifact is null)
            return RetryableServiceUnavailable();

        try
        {
            return await CreateValidatedSitemapResultAsync(rootArtifact, store);
        }
        finally
        {
            rootArtifact.Dispose();
        }
    }

    private async Task<IActionResult> CreateValidatedSitemapResultAsync(
        ValidatedSitemapArtifact artifact,
        Store store)
    {
        if (artifact.Catalog.Kind == SitemapCatalogKind.SitemapIndex)
            return File(artifact.DetachStream(), MimeTypes.ApplicationXml);
        if (!artifact.Catalog.HasAlternateLinks)
            return File(artifact.DetachStream(), MimeTypes.ApplicationXml);

        await using var source = artifact.DetachStream();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        if (!SitemapCanonicalOrigin.TryCreate(store.Url, _webHelper.GetStoreLocation(), out var canonicalStoreOrigin))
            return RetryableServiceUnavailable();
        try
        {
            var responseStream = await _sitemapHreflangStreamTransformer.TransformToTemporaryFileAsync(
                source, canonicalStoreOrigin, languages, store.DefaultLanguageId,
                SitemapProtocolMaxBytes, HttpContext.RequestAborted);
            return File(responseStream, MimeTypes.ApplicationXml);
        }
        catch (SitemapHreflangTransformException)
        {
            return RetryableServiceUnavailable();
        }
    }

    [HttpGet]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> BlogRss(int languageId)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var language = await _languageService.GetLanguageByIdAsync(languageId)
            ?? await _languageService.GetLanguageByIdAsync(store.DefaultLanguageId);
        languageId = language?.Id ?? store.DefaultLanguageId;

        var storeName = await _localizationService.GetLocalizedAsync(store, entity => entity.Name,
            languageId, ensureTwoPublishedLanguages: false);
        var blogLabel = await _localizationService.GetResourceAsync("Blog", languageId,
            logIfNotFound: false, defaultValue: "Blog");
        var feed = new RssFeed($"{storeName}: {blogLabel}", blogLabel,
            new Uri(_webHelper.GetStoreLocation()), DateTime.UtcNow);

        if (_blogSettings.Enabled && language is not null)
        {
            var posts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId);
            foreach (var post in posts)
            {
                var slug = await _blogLocalizationService.GetSlugAsync(post, languageId);
                var title = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Title), languageId, post.Title);
                var body = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Body), languageId, post.Body);
                var url = BuildAbsoluteLocalizedUrl(language.UniqueSeoCode, slug);
                feed.Items.Add(new RssItem(title, body, new Uri(url),
                    $"urn:store:{store.Id}:blog:post:{post.Id}", post.CreatedOnUtc));
            }
        }

        return new RssActionResult(feed, _webHelper.GetThisPageUrl(includeQueryString: false));
    }

    [HttpGet]
    public async Task<IActionResult> RedirectLegacyContactUs(string language)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var languages = await _languageService.GetAllLanguagesAsync(storeId: store.Id);
        var requestedLanguage = languages.FirstOrDefault(item => item.Published &&
            item.UniqueSeoCode.Equals(language, StringComparison.OrdinalIgnoreCase))
            ?? languages.FirstOrDefault(item => item.Id == store.DefaultLanguageId)
            ?? languages.FirstOrDefault();

        var topic = await _topicService.GetTopicBySystemNameAsync("ContactUs", store.Id);
        if (topic is not null && requestedLanguage is not null)
        {
            var slug = await _urlRecordService.GetSeNameAsync(topic.Id, "Topic", requestedLanguage.Id,
                returnDefaultValue: true, ensureTwoPublishedLanguages: false);
            if (!string.IsNullOrWhiteSpace(slug) &&
                !slug.Equals("contactus", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectPermanent(BuildLocalizedPath(requestedLanguage.UniqueSeoCode, slug));
            }
        }

        // Fail safely through the current core contact view when a language
        // package has not supplied a localized contact alias.
        var model = await _commonModelFactory.PrepareContactUsModelAsync(new ContactUsModel(), false);
        return View("~/Views/Common/ContactUs.cshtml", model);
    }

    private string BuildAbsoluteLocalizedUrl(string languageCode, string slug)
    {
        return $"{_webHelper.GetStoreLocation().TrimEnd('/')}{BuildLocalizedPath(languageCode, slug)}";
    }

    private static string BuildLocalizedPath(string languageCode, string slug)
    {
        return $"/{languageCode}/{slug}";
    }

    private string GetExpectedSitemapPath(int storeId, int languageId, int id)
    {
        var fileName = string.Format(CultureInfo.InvariantCulture,
            NopSeoDefaults.SitemapXmlFilePattern, storeId, languageId, id);

        return _nopFileProvider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory, fileName);
    }

    private async Task<ValidatedSitemapArtifact> TryOpenValidatedSitemapAsync(
        string candidatePath,
        string expectedPath,
        Uri canonicalRoot,
        SitemapCatalogKind requiredKind = SitemapCatalogKind.Unknown)
    {
        if (!TryOpenSitemap(candidatePath, expectedPath, out var stream))
            return null;

        var transferOwnership = false;
        try
        {
            if (stream.Length > SitemapProtocolMaxBytes)
                return null;

            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                CloseInput = false,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = SitemapProtocolMaxBytes
            });

            if (await reader.MoveToContentAsync() != XmlNodeType.Element ||
                reader.Depth != 0 ||
                !reader.NamespaceURI.Equals(SitemapXmlNamespace, StringComparison.Ordinal))
            {
                return null;
            }

            var rootKind = reader.LocalName switch
            {
                "urlset" => SitemapCatalogKind.UrlSet,
                "sitemapindex" => SitemapCatalogKind.SitemapIndex,
                _ => SitemapCatalogKind.Unknown
            };

            if (rootKind == SitemapCatalogKind.Unknown ||
                requiredKind != SitemapCatalogKind.Unknown && rootKind != requiredKind ||
                rootKind == SitemapCatalogKind.SitemapIndex && canonicalRoot is null)
            {
                return null;
            }

            var partIds = new HashSet<int>();
            var locations = new List<Uri>();
            var entryCount = 0;
            var urlLocations = 0;
            var hasAlternateLinks = false;
            var rootClosed = false;
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 0)
                {
                    rootClosed = true;
                    continue;
                }

                if (rootClosed)
                {
                    if (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or
                        XmlNodeType.Comment or XmlNodeType.ProcessingInstruction)
                        continue;
                    return null;
                }

                if (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or
                    XmlNodeType.Comment or XmlNodeType.ProcessingInstruction)
                    continue;

                if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1 ||
                    !reader.NamespaceURI.Equals(SitemapXmlNamespace, StringComparison.Ordinal))
                    return null;

                var expectedEntryName = rootKind == SitemapCatalogKind.UrlSet ? "url" : "sitemap";
                if (!reader.LocalName.Equals(expectedEntryName, StringComparison.Ordinal) ||
                    ++entryCount > SitemapProtocolMaxEntries)
                    return null;

                var entryDepth = reader.Depth;
                var locSeen = false;
                var optionalSiblingsSeen = new HashSet<string>(StringComparer.Ordinal);
                if (!reader.IsEmptyElement)
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == entryDepth)
                            break;

                        if (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or
                            XmlNodeType.Comment or XmlNodeType.ProcessingInstruction)
                            continue;

                        if (reader.NodeType != XmlNodeType.Element || reader.Depth != entryDepth + 1 ||
                            (!reader.NamespaceURI.Equals(SitemapXmlNamespace, StringComparison.Ordinal) &&
                             !(rootKind == SitemapCatalogKind.UrlSet &&
                               reader.NamespaceURI.Equals("http://www.w3.org/1999/xhtml", StringComparison.Ordinal) &&
                               reader.LocalName.Equals("link", StringComparison.Ordinal))))
                            return null;

                        var isSitemapNamespace = reader.NamespaceURI.Equals(SitemapXmlNamespace,
                            StringComparison.Ordinal);
                        var isLoc = isSitemapNamespace && reader.LocalName.Equals("loc", StringComparison.Ordinal);
                        var isAllowedSibling = isSitemapNamespace &&
                            (reader.LocalName.Equals("lastmod", StringComparison.Ordinal) ||
                             rootKind == SitemapCatalogKind.UrlSet &&
                             (reader.LocalName.Equals("changefreq", StringComparison.Ordinal) ||
                              reader.LocalName.Equals("priority", StringComparison.Ordinal))) ||
                            rootKind == SitemapCatalogKind.UrlSet &&
                             reader.NamespaceURI.Equals("http://www.w3.org/1999/xhtml", StringComparison.Ordinal) &&
                             reader.LocalName.Equals("link", StringComparison.Ordinal);

                        if (rootKind == SitemapCatalogKind.UrlSet &&
                            reader.NamespaceURI.Equals("http://www.w3.org/1999/xhtml", StringComparison.Ordinal) &&
                            reader.LocalName.Equals("link", StringComparison.Ordinal))
                        {
                            hasAlternateLinks = true;
                        }

                        if (!isLoc && !isAllowedSibling)
                            return null;

                        if (isAllowedSibling &&
                            reader.NamespaceURI.Equals(SitemapXmlNamespace, StringComparison.Ordinal) &&
                            !optionalSiblingsSeen.Add(reader.LocalName))
                            return null;

                        if (!isLoc)
                        {
                            if (!reader.IsEmptyElement)
                            {
                                var siblingDepth = reader.Depth;
                                while (await reader.ReadAsync())
                                {
                                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == siblingDepth)
                                        break;
                                    if (reader.NodeType == XmlNodeType.Element)
                                        return null;
                                }
                            }
                            continue;
                        }

                        if (locSeen || reader.IsEmptyElement)
                            return null;

                        locSeen = true;
                        var locDepth = reader.Depth;
                        var locationBuilder = new StringBuilder();
                        while (await reader.ReadAsync())
                        {
                            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == locDepth)
                                break;
                            if (reader.NodeType == XmlNodeType.Element)
                                return null;
                            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                                XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                                locationBuilder.Append(reader.Value);
                        }

                        var location = locationBuilder.ToString().Trim();
                        if (rootKind == SitemapCatalogKind.UrlSet)
                        {
                            if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) ||
                                !IsStoreLocation(uri, canonicalRoot))
                                return null;
                            urlLocations++;
                        }
                        else
                        {
                            if (!TryGetSitemapPart(location, canonicalRoot,
                                    out var partId, out var sitemapLocation) || !partIds.Add(partId))
                                return null;
                            locations.Add(sitemapLocation);
                        }
                    }
                }

                if (!locSeen)
                    return null;
            }

            if (!rootClosed || rootKind == SitemapCatalogKind.SitemapIndex && entryCount == 0)
                return null;

            if (rootKind == SitemapCatalogKind.UrlSet && urlLocations == 0)
                return null;

            // Reaching EOF on this same handle proves completeness. Rewind and
            // transfer precisely that validated handle to MVC when it is served.
            stream.Position = 0;
            transferOwnership = true;
            return new ValidatedSitemapArtifact(stream,
                new SitemapCatalog(rootKind, partIds, locations, hasAlternateLinks));
        }
        catch (XmlException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
        finally
        {
            // Successful artifacts own the stream after this method returns.
            if (!transferOwnership)
                await stream.DisposeAsync();
        }
    }

    private async Task<IActionResult> TryCreateValidatedPartResultAsync(string expectedPath,
        Store store,
        Uri canonicalRoot)
    {
        var artifact = await TryOpenValidatedSitemapAsync(expectedPath, expectedPath,
            canonicalRoot, SitemapCatalogKind.UrlSet);
        if (artifact is null)
            return null;

        try
        {
            return await CreateValidatedSitemapResultAsync(artifact, store);
        }
        finally
        {
            artifact.Dispose();
        }
    }

    private static bool TryGetSitemapPart(string location, Uri canonicalRoot,
        out int id, out Uri sitemapLocation)
    {
        id = 0;
        sitemapLocation = null;
        if (canonicalRoot is null || string.IsNullOrWhiteSpace(location) ||
            !Uri.TryCreate(location.Trim(), UriKind.Absolute, out var uri) ||
            !IsSameOrigin(uri, canonicalRoot) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var rootPath = canonicalRoot.AbsolutePath;
        var separatorIndex = rootPath.LastIndexOf('/');
        if (separatorIndex < 0)
            return false;

        var directoryPath = rootPath[..(separatorIndex + 1)];
        if (!uri.AbsolutePath.StartsWith(directoryPath, StringComparison.Ordinal))
            return false;

        var fileName = uri.AbsolutePath[directoryPath.Length..];
        var match = PublicSitemapFileNamePattern.Match(fileName);
        if (!match.Success || !int.TryParse(match.Groups["id"].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out id))
        {
            return false;
        }

        sitemapLocation = uri;
        return true;
    }

    private ContentResult CreateSitemapIndexAlias(IEnumerable<Uri> locations)
    {
        XNamespace sitemap = SitemapXmlNamespace;
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(sitemap + "sitemapindex",
                locations.Select(location =>
                    new XElement(sitemap + "sitemap",
                        new XElement(sitemap + "loc", location.AbsoluteUri)))));

        var result = Content(document.ToString(SaveOptions.DisableFormatting),
            "application/xml; charset=utf-8", Encoding.UTF8);
        result.StatusCode = StatusCodes.Status200OK;
        return result;
    }

    private IActionResult RetryableServiceUnavailable()
    {
        Response.Headers["Retry-After"] = Math.Max(1, _sitemapXmlSettings.SitemapBuildOperationDelay)
            .ToString(CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static bool IsSameOrigin(Uri left, Uri right)
    {
        return IsHttpScheme(left.Scheme) &&
               left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               left.Port == right.Port;
    }

    private static bool IsStoreLocation(Uri location, Uri canonicalRoot)
    {
        if (canonicalRoot is null || !IsSameOrigin(location, canonicalRoot) ||
            !string.IsNullOrEmpty(location.UserInfo) || !string.IsNullOrEmpty(location.Fragment))
        {
            return false;
        }

        var separatorIndex = canonicalRoot.AbsolutePath.LastIndexOf('/');
        if (separatorIndex < 0)
            return false;

        var storeDirectory = canonicalRoot.AbsolutePath[..(separatorIndex + 1)];
        if (storeDirectory.Equals("/", StringComparison.Ordinal))
            return location.AbsolutePath.StartsWith("/", StringComparison.Ordinal);

        return location.AbsolutePath.Equals(storeDirectory.TrimEnd('/'), StringComparison.Ordinal) ||
               location.AbsolutePath.StartsWith(storeDirectory, StringComparison.Ordinal);
    }

    private static bool IsHttpScheme(string scheme)
    {
        return scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryOpenSitemap(string candidatePath, string expectedPath, out FileStream stream)
    {
        stream = null;

        if (!IsExpectedSitemapPath(candidatePath, expectedPath, out var fullPath))
            return false;

        try
        {
            var candidate = new FileStream(fullPath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                // Deny in-place writes so validation and response observe the
                // same bytes. Allow delete sharing because core atomically
                // replaces old artifacts; this handle still retains old bytes.
                Share = FileShare.Read | FileShare.Delete,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

            if (candidate.Length <= 0)
            {
                candidate.Dispose();
                return false;
            }

            stream = candidate;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    private bool IsExpectedSitemapPath(string candidatePath, string expectedPath, out string fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(expectedPath))
            return false;

        try
        {
            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                _nopFileProvider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory)));
            var candidate = Path.GetFullPath(candidatePath);
            var expected = Path.GetFullPath(expectedPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!string.Equals(Path.GetDirectoryName(candidate), directory, comparison) ||
                !string.Equals(Path.GetDirectoryName(expected), directory, comparison) ||
                !SitemapFileNamePattern.IsMatch(Path.GetFileName(candidate)) ||
                !SitemapFileNamePattern.IsMatch(Path.GetFileName(expected)) ||
                !string.Equals(candidate, expected, comparison))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
    }

    private enum SitemapCatalogKind
    {
        Unknown,
        UrlSet,
        SitemapIndex
    }

    private readonly record struct SitemapCatalog(SitemapCatalogKind Kind,
        HashSet<int> PartIds, IReadOnlyList<Uri> Locations, bool HasAlternateLinks)
    {
        public static SitemapCatalog Unknown { get; } =
            new(SitemapCatalogKind.Unknown, new HashSet<int>(), Array.Empty<Uri>(), false);
    }

    private sealed class ValidatedSitemapArtifact : IDisposable
    {
        private FileStream _stream;

        public ValidatedSitemapArtifact(FileStream stream, SitemapCatalog catalog)
        {
            _stream = stream;
            Catalog = catalog;
        }

        public SitemapCatalog Catalog { get; }

        public FileStream DetachStream()
        {
            var stream = _stream ?? throw new InvalidOperationException(
                "The validated sitemap stream has already been detached.");
            _stream = null;
            return stream;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
