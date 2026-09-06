using System.Text;
using System.Xml.Linq;
using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapHreflangStreamTransformerTests
{
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    [Test]
    public async Task RewritesRegionalCulturesAndAddsExactlyOneDefaultWithoutChangingSource()
    {
        var sourceXml = $"<urlset xmlns=\"{SitemapNamespace}\" xmlns:xhtml=\"{XhtmlNamespace}\">" +
            "<url><loc>https://hoodarcheryshop.com/shop/gb/longbow</loc>" +
            "<xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/shop/en/longbow\" />" +
            "<xhtml:link rel=\"alternate\" hreflang=\"gb\" href=\"https://hoodarcheryshop.com/shop/gb/longbow\" />" +
            "<xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"https://stale.example/\" />" +
            "<changefreq>weekly</changefreq><lastmod>2026-08-27</lastmod></url></urlset>";
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceXml));
        var transformer = new SitemapHreflangStreamTransformer();

        await using var result = await transformer.TransformToTemporaryFileAsync(source,
            new Uri("https://hoodarcheryshop.com/shop/"), Languages(), 1, 1024 * 1024);
        var temporaryPath = result.Name;
        var document = await XDocument.LoadAsync(result, LoadOptions.None, CancellationToken.None);

        XNamespace xhtml = XhtmlNamespace;
        Assert.Multiple(() =>
        {
            Assert.That(document.Descendants(xhtml + "link")
                .Select(link => (link.Attribute("hreflang")?.Value, link.Attribute("href")?.Value)),
                Is.EqualTo(new[]
                {
                    ("en-US", "https://hoodarcheryshop.com/shop/en/longbow"),
                    ("en-GB", "https://hoodarcheryshop.com/shop/gb/longbow"),
                    ("x-default", "https://hoodarcheryshop.com/shop/en/longbow")
                }));
            Assert.That(Encoding.UTF8.GetString(source.ToArray()), Is.EqualTo(sourceXml));
            Assert.That(File.Exists(temporaryPath), Is.True);
        });

        await result.DisposeAsync();
        Assert.That(File.Exists(temporaryPath), Is.False,
            "The transformed response artifact must be delete-on-close.");
    }

    [Test]
    public void UnknownOrForeignAlternateFailsClosed()
    {
        var sourceXml = $"<urlset xmlns=\"{SitemapNamespace}\" xmlns:xhtml=\"{XhtmlNamespace}\">" +
            "<url><loc>https://hoodarcheryshop.com/en/longbow</loc>" +
            "<xhtml:link rel=\"alternate\" hreflang=\"zz\" href=\"https://hoodarcheryshop.com/zz/longbow\" />" +
            "</url></urlset>";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceXml));

        Assert.ThrowsAsync<SitemapHreflangTransformException>(async () =>
            await new SitemapHreflangStreamTransformer().TransformToTemporaryFileAsync(source,
                new Uri("https://hoodarcheryshop.com/"), Languages(), 1, 1024 * 1024));
    }

    [Test]
    public async Task HalloweenUsesItsAuthoredFallbackWhenStoreDefaultHasNoLandingCopy()
    {
        var landing = "halloween-archery-and-costume-guide";
        var sourceXml = $"<urlset xmlns=\"{SitemapNamespace}\" xmlns:xhtml=\"{XhtmlNamespace}\">" +
            $"<url><loc>https://hoodarcheryshop.com/en/{landing}</loc>" +
            $"<xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/en/{landing}\" />" +
            $"<xhtml:link rel=\"alternate\" hreflang=\"de\" href=\"https://hoodarcheryshop.com/de/{landing}\" />" +
            "</url></urlset>";
        var languages = new[]
        {
            new Language
            {
                Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true
            },
            new Language
            {
                Id = 2, UniqueSeoCode = "de", LanguageCulture = "de-DE", Published = true
            }
        };
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceXml));

        await using var result = await new SitemapHreflangStreamTransformer()
            .TransformToTemporaryFileAsync(source, new Uri("https://hoodarcheryshop.com/"),
                languages, defaultLanguageId: 99, maximumOutputBytes: 1024 * 1024);
        var document = await XDocument.LoadAsync(result, LoadOptions.None, CancellationToken.None);
        XNamespace xhtml = XhtmlNamespace;

        var xDefault = document.Descendants(xhtml + "link").Single(link =>
            link.Attribute("hreflang")?.Value == "x-default");
        Assert.That(xDefault.Attribute("href")?.Value,
            Is.EqualTo($"https://hoodarcheryshop.com/en/{landing}"));
    }

    [Test]
    public void OutputBeyondProtocolLimitFailsBeforeAResponseStreamIsReturned()
    {
        var sourceXml = $"<urlset xmlns=\"{SitemapNamespace}\" xmlns:xhtml=\"{XhtmlNamespace}\">" +
            "<url><loc>https://hoodarcheryshop.com/en/longbow</loc>" +
            "<xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/en/longbow\" />" +
            "</url></urlset>";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceXml));

        Assert.ThrowsAsync<SitemapHreflangTransformException>(async () =>
            await new SitemapHreflangStreamTransformer().TransformToTemporaryFileAsync(source,
                new Uri("https://hoodarcheryshop.com/"), Languages(), 1, 100));
    }

    [Test]
    [NonParallelizable]
    public void CancellationClosesAndDeletesTheTemporaryArtifact()
    {
        var before = Directory.GetFiles(Path.GetTempPath(), "hood-sitemap-hreflang-*.tmp")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceXml = $"<urlset xmlns=\"{SitemapNamespace}\" xmlns:xhtml=\"{XhtmlNamespace}\">" +
            "<url><loc>https://hoodarcheryshop.com/en/longbow</loc>" +
            "<xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/en/longbow\" />" +
            "</url></urlset>";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(sourceXml));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SitemapHreflangStreamTransformer().TransformToTemporaryFileAsync(source,
                new Uri("https://hoodarcheryshop.com/"), Languages(), 1, 1024 * 1024,
                cancellation.Token));

        var after = Directory.GetFiles(Path.GetTempPath(), "hood-sitemap-hreflang-*.tmp")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.That(after.Except(before), Is.Empty);
    }

    private static Language[] Languages() =>
    [
        new Language
        {
            Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true
        },
        new Language
        {
            Id = 2, UniqueSeoCode = "gb", LanguageCulture = "en-GB", Published = true
        }
    ];
}
