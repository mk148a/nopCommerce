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
using Nop.Core.Infrastructure;
using Nop.Core.Rss;
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
        _sitemapModelFactory = sitemapModelFactory;
        _storeContext = storeContext;
        _topicService = topicService;
        _urlRecordService = urlRecordService;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    // Numbered sitemap guard. The unnumbered sitemap.xml route remains in core.
    [HttpGet]
    [CheckAccessClosedStore(ignore: true)]
    [CheckAccessPublicStore(ignore: true)]
    [CheckLanguageSeoCode(ignore: true)]
    public async Task<IActionResult> SitemapXml(int id)
    {
        if (!_sitemapXmlSettings.SitemapXmlEnabled)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (id < 1)
            return NotFound();

        var store = await _storeContext.GetCurrentStoreAsync();
        var language = await _workContext.GetWorkingLanguageAsync();
        var expectedPath = GetExpectedSitemapPath(store.Id, language.Id, id);
        var rootPath = GetExpectedSitemapPath(store.Id, language.Id, 0);
        var hasCanonicalRoot = TryGetCanonicalSitemapUri(store.Url, out var canonicalRoot);
        var rootArtifact = await TryOpenValidatedSitemapAsync(rootPath, rootPath,
            hasCanonicalRoot ? canonicalRoot : null);

        if (rootArtifact is null)
        {
            // Every cold numbered request first participates in the one core
            // id=0 lock. This prevents independent id=1..N generation storms
            // and establishes the canonical root before part routing.
            try
            {
                var rootModel = await _sitemapModelFactory.PrepareSitemapXmlModelAsync(0);
                rootArtifact = await TryOpenValidatedSitemapAsync(rootModel?.SitemapXmlPath,
                    rootPath, hasCanonicalRoot ? canonicalRoot : null);
            }
            catch (InvalidOperationException)
            {
                // Another request owns the canonical generation lock. A
                // complete, protocol-valid old part remains a safe LKG response.
                return await TryCreateValidatedPartResultAsync(expectedPath)
                    ?? RetryableServiceUnavailable();
            }

            if (rootArtifact is null)
            {
                return await TryCreateValidatedPartResultAsync(expectedPath)
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
                    return File(rootArtifact.DetachStream(), MimeTypes.ApplicationXml);
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
                expectedPath, canonicalRoot: null, SitemapCatalogKind.UrlSet);
            if (partArtifact is not null)
                return File(partArtifact.DetachStream(), MimeTypes.ApplicationXml);

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
            return await TryCreateValidatedPartResultAsync(expectedPath)
                ?? RetryableServiceUnavailable();
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
            while (await reader.ReadAsync())
            {
                if (reader.NodeType != XmlNodeType.Element ||
                    !reader.NamespaceURI.Equals(SitemapXmlNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                if (rootKind == SitemapCatalogKind.UrlSet &&
                    reader.Depth == 1 && reader.LocalName.Equals("url", StringComparison.Ordinal))
                {
                    if (++entryCount > SitemapProtocolMaxEntries)
                        return null;
                    continue;
                }

                if (rootKind == SitemapCatalogKind.SitemapIndex &&
                    reader.Depth == 2 && reader.LocalName.Equals("loc", StringComparison.Ordinal))
                {
                    var location = await reader.ReadElementContentAsStringAsync();
                    if (!TryGetSitemapPart(location, canonicalRoot,
                            out var partId, out var sitemapLocation) ||
                        !partIds.Add(partId) || ++entryCount > SitemapProtocolMaxEntries)
                    {
                        return null;
                    }

                    locations.Add(sitemapLocation);
                }
            }

            if (rootKind == SitemapCatalogKind.SitemapIndex && entryCount == 0)
                return null;

            // Reaching EOF on this same handle proves completeness. Rewind and
            // transfer precisely that validated handle to MVC when it is served.
            stream.Position = 0;
            transferOwnership = true;
            return new ValidatedSitemapArtifact(stream,
                new SitemapCatalog(rootKind, partIds, locations));
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

    private async Task<FileStreamResult> TryCreateValidatedPartResultAsync(string expectedPath)
    {
        var artifact = await TryOpenValidatedSitemapAsync(expectedPath, expectedPath,
            canonicalRoot: null, SitemapCatalogKind.UrlSet);
        return artifact is null
            ? null
            : File(artifact.DetachStream(), MimeTypes.ApplicationXml);
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

    private static bool TryGetCanonicalSitemapUri(string storeUrl, out Uri canonicalRoot)
    {
        canonicalRoot = null;
        if (!Uri.TryCreate(storeUrl, UriKind.Absolute, out var storeUri) ||
            !IsHttpScheme(storeUri.Scheme) ||
            string.IsNullOrWhiteSpace(storeUri.Host) ||
            !string.IsNullOrEmpty(storeUri.UserInfo) ||
            !string.IsNullOrEmpty(storeUri.Query) ||
            !string.IsNullOrEmpty(storeUri.Fragment))
        {
            return false;
        }

        var origin = storeUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        var pathBase = storeUri.AbsolutePath.TrimEnd('/');
        var sitemapPath = $"{pathBase}{SitemapFallbackPath}";
        return Uri.TryCreate($"{origin.TrimEnd('/')}{sitemapPath}",
            UriKind.Absolute, out canonicalRoot);
    }

    private static bool IsSameOrigin(Uri left, Uri right)
    {
        return IsHttpScheme(left.Scheme) &&
               left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               left.IdnHost.Equals(right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               left.Port == right.Port;
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
        HashSet<int> PartIds, IReadOnlyList<Uri> Locations)
    {
        public static SitemapCatalog Unknown { get; } =
            new(SitemapCatalogKind.Unknown, new HashSet<int>(), Array.Empty<Uri>());
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
