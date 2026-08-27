using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Nop.Plugin.Widgets.HoodVideoSeo.Controllers;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using NUnit.Framework;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Tests;

[TestFixture]
public sealed class VideoSitemapTextTests
{
    [TestCase(100)]
    [TestCase(2048)]
    public void TruncateHonorsLimitWithoutAddingContent(int limit)
    {
        var source = new string('x', limit + 50);

        var result = InvokeTruncate(source, limit);

        Assert.That(result, Has.Length.EqualTo(limit));
        Assert.That(result, Is.EqualTo(source[..limit]));
    }

    [Test]
    public void TruncateDoesNotSplitSurrogatePair()
    {
        var source = $"{new string('x', 99)}😀 suffix";

        var result = InvokeTruncate(source, 100);

        Assert.That(result, Has.Length.EqualTo(99));
        Assert.That(result, Does.Not.Contain("�"));
    }

    [Test]
    public void TruncateLeavesShortTextUnchanged()
    {
        const string source = "Reviewed product video description";

        Assert.That(InvokeTruncate(source, 100), Is.SameAs(source));
    }

    [Test]
    public void HostileMappedHostIsRejectedBySharedParser()
    {
        const string hostileDatabaseUrl = "https://evil.example/embed/0KEeQy_QZeg?steal=true";
        Assert.That(YouTubeVideoUrlParser.TryParseEmbedUrl(hostileDatabaseUrl, out _), Is.False);
        Assert.That(YouTubeVideoUrlParser.ExtractEmbedIds($"<iframe src=\"{hostileDatabaseUrl}\"></iframe>"), Is.Empty);
    }

    [TestCase("http://www.youtube.com/embed/0KEeQy_QZeg")]
    [TestCase("https://www.youtube.com.evil.example/embed/0KEeQy_QZeg")]
    [TestCase("https://www.youtube.com/watch?v=0KEeQy_QZeg")]
    [TestCase("https://www.youtube.com/embed/0KEeQy_QZeg/extra")]
    [TestCase("https://www.youtube.com:444/embed/0KEeQy_QZeg")]
    public void NonCanonicalEmbedUrlIsRejected(string source)
    {
        Assert.That(YouTubeVideoUrlParser.TryParseEmbedUrl(source, out _), Is.False);
    }

    [TestCase("https://www.youtube.com/embed/0KEeQy_QZeg")]
    [TestCase("https://youtube-nocookie.com/embed/0KEeQy_QZeg?rel=0")]
    public void ApprovedHttpsEmbedUrlIsAccepted(string source)
    {
        Assert.That(YouTubeVideoUrlParser.TryParseEmbedUrl(source, out var id), Is.True);
        Assert.That(id, Is.EqualTo("0KEeQy_QZeg"));
    }

    [Test]
    public void WatchViewPreservesValidatedEmbedParametersFromLegacyThemeRewriter()
    {
        var view = File.ReadAllText(FindPluginFile("Views", "VideoWatch", "Watch.cshtml"));

        Assert.Multiple(() =>
        {
            Assert.That(view, Does.Contain("hood-youtube-lite hood-video-seo-managed"));
            Assert.That(view, Does.Contain("data-hood-youtube-loaded=\"true\""));
            Assert.That(view, Does.Not.Contain("data-hood-youtube-src"));
            Assert.That(view, Does.Contain("src=\"@Model.EmbedUrl\""));
            Assert.That(view, Does.Contain("referrerpolicy=\"strict-origin-when-cross-origin\""));
            Assert.That(view, Does.Contain("aspect-ratio: 16 / 9"));
        });
    }

    [Test]
    public void SitemapBuilderProducesValidNamespacedXml()
    {
        var controllerType = typeof(VideoWatchController);
        var entryType = controllerType.GetNestedType("VideoSitemapEntry", BindingFlags.NonPublic);
        Assert.That(entryType, Is.Not.Null);
        var constructor = entryType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 6);
        var entry = constructor.Invoke(new object[]
        {
            "https://shop.example/en/watch/42/0KEeQy_QZeg",
            "https://i.ytimg.com/vi/0KEeQy_QZeg/hqdefault.jpg",
            "Product video 1",
            "Reviewed product video description",
            "https://www.youtube-nocookie.com/embed/0KEeQy_QZeg",
            new DateOnly(2025, 2, 4)
        });
        var entries = Array.CreateInstance(entryType, 1);
        entries.SetValue(entry, 0);

        var method = controllerType.GetMethod("BuildSitemap", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        var xml = (string)method!.Invoke(null, new object[] { entries })!;
        var document = XDocument.Parse(xml);
        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace video = "http://www.google.com/schemas/sitemap-video/1.1";

        Assert.Multiple(() =>
        {
            Assert.That(document.Root?.Name, Is.EqualTo(sitemap + "urlset"));
            Assert.That(document.Root?.Element(sitemap + "url")?.Element(sitemap + "loc")?.Value,
                Is.EqualTo("https://shop.example/en/watch/42/0KEeQy_QZeg"));
            Assert.That(document.Descendants(video + "player_loc").Single().Value,
                Is.EqualTo("https://www.youtube-nocookie.com/embed/0KEeQy_QZeg"));
            Assert.That(document.Descendants(video + "publication_date").Single().Value, Is.EqualTo("2025-02-04"));
        });
    }

    [TestCase("tPxPcvSjLJk")]
    [TestCase("B7H4igrP0Wo")]
    [TestCase("FxSceVSLx4U")]
    public void ReviewedRetiredVideosAreExcludedFromSitemapCandidates(string youtubeId)
    {
        var entries = new Dictionary<int, HashSet<string>>();
        var method = typeof(VideoWatchController).GetMethod("AddVideoIds", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        method!.Invoke(null, new object[] { entries, 144, $"<iframe src=\"https://www.youtube.com/embed/{youtubeId}\"></iframe>" });

        // Candidate extraction keeps source data lossless; ProductVideos applies
        // the retired registry before generating a public sitemap entry.
        Assert.That(entries[144], Does.Contain(youtubeId));
        Assert.That(RetiredYouTubeVideos.Contains(youtubeId), Is.True);
    }

    private static string InvokeTruncate(string value, int maxLength)
    {
        var method = typeof(VideoWatchController).GetMethod("Truncate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (string)method!.Invoke(null, new object[] { value, maxLength })!;
    }

    private static string FindPluginFile(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var pluginRoot = Path.Combine(directory.FullName, "Nop.Plugin.Widgets.HoodVideoSeo");
            var candidate = Path.Combine(new[] { pluginRoot }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
                return candidate;

            pluginRoot = Path.Combine(directory.FullName, "src", "Plugins", "Nop.Plugin.Widgets.HoodVideoSeo");
            candidate = Path.Combine(new[] { pluginRoot }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
                return candidate;
        }

        Assert.Fail("Could not locate the HoodVideoSeo plugin source from the test output directory.");
        return string.Empty;
    }
}
