using System.Text;
using System.Xml;
using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

public interface ISitemapHreflangStreamTransformer
{
    Task<FileStream> TransformToTemporaryFileAsync(Stream source,
        Uri canonicalStoreOrigin,
        IEnumerable<Language> languages,
        int defaultLanguageId,
        long maximumOutputBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rewrites a validated core sitemap into a delete-on-close response file.
/// The cached core artifact is never changed and XML is processed one node at
/// a time, so a multi-megabyte sitemap is not materialized as a string or DOM.
/// </summary>
internal sealed class SitemapHreflangStreamTransformer : ISitemapHreflangStreamTransformer
{
    internal const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    internal const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    public async Task<FileStream> TransformToTemporaryFileAsync(Stream source,
        Uri canonicalStoreOrigin,
        IEnumerable<Language> languages,
        int defaultLanguageId,
        long maximumOutputBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(canonicalStoreOrigin);
        ArgumentNullException.ThrowIfNull(languages);
        if (maximumOutputBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));

        var languageByCode = languages
            .Where(language => language?.Published == true &&
                !string.IsNullOrWhiteSpace(language.UniqueSeoCode) &&
                !string.IsNullOrWhiteSpace(language.LanguageCulture))
            .DistinctBy(language => language.UniqueSeoCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(language => language.UniqueSeoCode, StringComparer.OrdinalIgnoreCase);
        if (languageByCode.Count == 0)
            throw new SitemapHreflangTransformException("No published language has a usable hreflang culture.");

        if (source.CanSeek)
            source.Position = 0;

        var temporaryPath = Path.Combine(Path.GetTempPath(),
            $"hood-sitemap-hreflang-{Guid.NewGuid():N}.tmp");
        var output = new FileStream(temporaryPath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.Read,
            BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
        });

        var transferOwnership = false;
        try
        {
            await TransformAsync(source, output, canonicalStoreOrigin, languageByCode,
                defaultLanguageId, maximumOutputBytes, cancellationToken);
            await output.FlushAsync(cancellationToken);
            if (output.Length > maximumOutputBytes)
                throw new SitemapHreflangTransformException(
                    $"Transformed sitemap exceeds the {maximumOutputBytes} byte protocol limit.");

            output.Position = 0;
            transferOwnership = true;
            return output;
        }
        catch (SitemapHreflangTransformException)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
        {
            throw new SitemapHreflangTransformException("The sitemap hreflang stream could not be transformed.",
                exception);
        }
        finally
        {
            if (!transferOwnership)
                await output.DisposeAsync();
        }
    }

    private static async Task TransformAsync(Stream source,
        Stream output,
        Uri canonicalStoreOrigin,
        IReadOnlyDictionary<string, Language> languageByCode,
        int defaultLanguageId,
        long maximumOutputBytes,
        CancellationToken cancellationToken)
    {
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            Async = true,
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = maximumOutputBytes
        });
        await using var limitedOutput = new MaximumLengthWriteStream(output, maximumOutputBytes);
        await using var writer = XmlWriter.Create(limitedOutput, new XmlWriterSettings
        {
            Async = true,
            CloseOutput = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = false
        });

        var urlDepth = -1;
        var suppressedElementDepth = -1;
        var locationDepth = -1;
        string locationHref = null;
        string defaultHref = null;
        string firstAlternateHref = null;
        var alternateHrefs = new HashSet<string>(StringComparer.Ordinal);
        var alternateHreflangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allAlternatesAreHalloweenLandings = true;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (suppressedElementDepth >= 0)
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == suppressedElementDepth)
                    suppressedElementDepth = -1;
                continue;
            }

            switch (reader.NodeType)
            {
                case XmlNodeType.XmlDeclaration:
                    await writer.WriteStartDocumentAsync();
                    break;
                case XmlNodeType.Element:
                {
                    var isUrl = reader.NamespaceURI.Equals(SitemapNamespace, StringComparison.Ordinal) &&
                                reader.LocalName.Equals("url", StringComparison.Ordinal);
                    if (isUrl)
                    {
                        urlDepth = reader.Depth;
                        locationDepth = -1;
                        locationHref = null;
                        defaultHref = null;
                        firstAlternateHref = null;
                        alternateHrefs.Clear();
                        alternateHreflangs.Clear();
                        allAlternatesAreHalloweenLandings = true;
                    }

                    var isUrlLocation = urlDepth >= 0 && reader.Depth == urlDepth + 1 &&
                                        reader.NamespaceURI.Equals(SitemapNamespace, StringComparison.Ordinal) &&
                                        reader.LocalName.Equals("loc", StringComparison.Ordinal);
                    if (isUrlLocation)
                    {
                        locationDepth = reader.Depth;
                        locationHref = string.Empty;
                    }

                    var isAlternateLink = urlDepth >= 0 &&
                        reader.NamespaceURI.Equals(XhtmlNamespace, StringComparison.Ordinal) &&
                        reader.LocalName.Equals("link", StringComparison.Ordinal);
                    if (isAlternateLink)
                    {
                        var attributes = ReadAttributes(reader);
                        var rel = GetAttribute(attributes, "rel");
                        var hreflang = GetAttribute(attributes, "hreflang");
                        var href = GetAttribute(attributes, "href");
                        if (rel?.Equals("alternate", StringComparison.OrdinalIgnoreCase) == true &&
                            hreflang?.Equals("x-default", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (!reader.IsEmptyElement)
                                suppressedElementDepth = reader.Depth;
                            break;
                        }

                        if (rel?.Equals("alternate", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (!TryResolveLanguage(href, canonicalStoreOrigin, languageByCode,
                                    out var language))
                            {
                                throw new SitemapHreflangTransformException(
                                    $"Alternate sitemap URL '{href}' does not map to a published store language.");
                            }

                            SetAttribute(attributes, "hreflang", language.LanguageCulture);
                            if (!alternateHrefs.Add(href) || !alternateHreflangs.Add(language.LanguageCulture))
                            {
                                throw new SitemapHreflangTransformException(
                                    $"Duplicate alternate sitemap URL or culture '{href}'/'{language.LanguageCulture}' was encountered.");
                            }
                            firstAlternateHref ??= href;
                            allAlternatesAreHalloweenLandings &= IsHalloweenLandingUrl(href);
                            if (language.Id == defaultLanguageId)
                                defaultHref = href;
                        }

                        await WriteStartElementAsync(writer, reader, attributes);
                    }
                    else
                    {
                        await WriteStartElementAsync(writer, reader, ReadAttributes(reader));
                    }

                    if (reader.IsEmptyElement)
                    {
                        await writer.WriteEndElementAsync();
                        if (isUrlLocation)
                            locationDepth = -1;
                    }
                    break;
                }
                case XmlNodeType.EndElement:
                    if (locationDepth >= 0 && reader.Depth == locationDepth &&
                        reader.NamespaceURI.Equals(SitemapNamespace, StringComparison.Ordinal) &&
                        reader.LocalName.Equals("loc", StringComparison.Ordinal))
                    {
                        locationDepth = -1;
                    }
                    if (urlDepth >= 0 && reader.Depth == urlDepth &&
                        reader.NamespaceURI.Equals(SitemapNamespace, StringComparison.Ordinal) &&
                        reader.LocalName.Equals("url", StringComparison.Ordinal))
                    {
                        var normalizedLocationHref = locationHref?.Trim();
                        if (TryResolveLanguage(normalizedLocationHref, canonicalStoreOrigin, languageByCode,
                                out var locationLanguage))
                        {
                            // Core sitemaps may omit the alternate that represents the URL
                            // being described. Add it only when neither the culture nor the
                            // URL already occurs in the cluster, preserving authored subsets
                            // such as the five-localized Halloween landing.
                            if (!alternateHrefs.Contains(normalizedLocationHref) &&
                                !alternateHreflangs.Contains(locationLanguage.LanguageCulture))
                            {
                                await WriteAlternateAsync(writer, locationLanguage.LanguageCulture, normalizedLocationHref);
                                alternateHrefs.Add(normalizedLocationHref);
                                alternateHreflangs.Add(locationLanguage.LanguageCulture);
                            }

                            if (locationLanguage.Id == defaultLanguageId)
                                defaultHref ??= normalizedLocationHref;
                        }
                        if (string.IsNullOrWhiteSpace(defaultHref) &&
                            allAlternatesAreHalloweenLandings)
                        {
                            // Halloween exposes only locales with authored copy.
                            // Its shared target helper orders the store default,
                            // then English, then the first supported locale.
                            defaultHref = firstAlternateHref;
                        }
                        if (!string.IsNullOrWhiteSpace(defaultHref))
                            await WriteXDefaultAsync(writer, defaultHref);
                        urlDepth = -1;
                        locationHref = null;
                        defaultHref = null;
                    }
                    await writer.WriteFullEndElementAsync();
                    break;
                case XmlNodeType.Text:
                    if (locationDepth >= 0)
                        locationHref += reader.Value;
                    await writer.WriteStringAsync(reader.Value);
                    break;
                case XmlNodeType.CDATA:
                    if (locationDepth >= 0)
                        locationHref += reader.Value;
                    await writer.WriteCDataAsync(reader.Value);
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    await writer.WriteWhitespaceAsync(reader.Value);
                    break;
                case XmlNodeType.Comment:
                    await writer.WriteCommentAsync(reader.Value);
                    break;
                case XmlNodeType.ProcessingInstruction:
                    await writer.WriteProcessingInstructionAsync(reader.Name, reader.Value);
                    break;
                case XmlNodeType.EntityReference:
                    await writer.WriteEntityRefAsync(reader.Name);
                    break;
                case XmlNodeType.DocumentType:
                    throw new SitemapHreflangTransformException("DTD nodes are not allowed in sitemap XML.");
            }
        }

        await writer.FlushAsync();
    }

    private static List<XmlAttributeValue> ReadAttributes(XmlReader reader)
    {
        var attributes = new List<XmlAttributeValue>();
        if (!reader.HasAttributes)
            return attributes;

        while (reader.MoveToNextAttribute())
            attributes.Add(new XmlAttributeValue(reader.Prefix, reader.LocalName,
                reader.NamespaceURI, reader.Value));
        reader.MoveToElement();
        return attributes;
    }

    private static string GetAttribute(IEnumerable<XmlAttributeValue> attributes, string localName) =>
        attributes.FirstOrDefault(attribute => string.IsNullOrEmpty(attribute.NamespaceUri) &&
            attribute.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static void SetAttribute(IList<XmlAttributeValue> attributes, string localName, string value)
    {
        var index = attributes.ToList().FindIndex(attribute => string.IsNullOrEmpty(attribute.NamespaceUri) &&
            attribute.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
        var replacement = new XmlAttributeValue(string.Empty, localName, string.Empty, value);
        if (index >= 0)
            attributes[index] = replacement;
        else
            attributes.Add(replacement);
    }

    private static async Task WriteStartElementAsync(XmlWriter writer,
        XmlReader reader,
        IEnumerable<XmlAttributeValue> attributes)
    {
        await writer.WriteStartElementAsync(reader.Prefix, reader.LocalName, reader.NamespaceURI);
        foreach (var attribute in attributes)
            await writer.WriteAttributeStringAsync(attribute.Prefix, attribute.LocalName,
                attribute.NamespaceUri, attribute.Value);
    }

    private static async Task WriteXDefaultAsync(XmlWriter writer, string href)
    {
        await WriteAlternateAsync(writer, "x-default", href);
    }

    private static async Task WriteAlternateAsync(XmlWriter writer, string hreflang, string href)
    {
        await writer.WriteStartElementAsync("xhtml", "link", XhtmlNamespace);
        await writer.WriteAttributeStringAsync(null, "rel", null, "alternate");
        await writer.WriteAttributeStringAsync(null, "hreflang", null, hreflang);
        await writer.WriteAttributeStringAsync(null, "href", null, href);
        await writer.WriteEndElementAsync();
    }

    private static bool TryResolveLanguage(string href,
        Uri canonicalStoreOrigin,
        IReadOnlyDictionary<string, Language> languageByCode,
        out Language language)
    {
        language = null;
        if (string.IsNullOrWhiteSpace(href) ||
            !Uri.TryCreate(href, UriKind.Absolute, out var target) ||
            !target.Scheme.Equals(canonicalStoreOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !target.IdnHost.Equals(canonicalStoreOrigin.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            target.Port != canonicalStoreOrigin.Port)
        {
            return false;
        }

        var basePath = canonicalStoreOrigin.AbsolutePath.Trim('/');
        var targetPath = target.AbsolutePath.Trim('/');
        if (!string.IsNullOrEmpty(basePath))
        {
            if (!targetPath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            targetPath = targetPath[(basePath.Length + 1)..];
        }

        var firstSeparator = targetPath.IndexOf('/');
        var encodedCode = firstSeparator < 0 ? targetPath : targetPath[..firstSeparator];
        try
        {
            var code = Uri.UnescapeDataString(encodedCode);
            return languageByCode.TryGetValue(code, out language);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool IsHalloweenLandingUrl(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        try
        {
            return Uri.UnescapeDataString(segments[^1]).Equals(HalloweenLandingRoute.Slug,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private sealed record XmlAttributeValue(string Prefix, string LocalName,
        string NamespaceUri, string Value);

    private sealed class MaximumLengthWriteStream(Stream inner, long maximumLength) : Stream, IAsyncDisposable
    {
        private long _length;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(count);
            inner.Write(buffer, offset, count);
            _length += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            EnsureCapacity(buffer.Length);
            await inner.WriteAsync(buffer, cancellationToken);
            _length += buffer.Length;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Flush();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await FlushAsync();
            GC.SuppressFinalize(this);
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (_length > maximumLength - additionalBytes)
                throw new SitemapHreflangTransformException(
                    $"Transformed sitemap exceeds the {maximumLength} byte protocol limit.");
        }
    }
}

internal sealed class SitemapHreflangTransformException : Exception
{
    internal SitemapHreflangTransformException(string message) : base(message)
    {
    }

    internal SitemapHreflangTransformException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
