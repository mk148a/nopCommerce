using FluentAssertions;
using Moq;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Seo;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapArtifactInvalidatorTests
{
    [Test]
    public void DeletesOnlyCoreGeneratedXmlSitemapFiles()
    {
        const string directory = @"C:\site\sitemaps";
        var files = new[]
        {
            @"C:\site\sitemaps\sitemap-1-2-0.xml",
            @"C:\site\sitemaps\SITEMAP-12-24-7.XML",
            @"C:\site\sitemaps\sitemap-custom.xml",
            @"C:\site\sitemaps\sitemap-1-2-0.xml.br",
            @"C:\site\sitemaps\sitemap-1-2-0.xml.gz",
            @"C:\site\sitemaps\Index.htm"
        };
        var fileProvider = new Mock<INopFileProvider>();
        fileProvider.Setup(provider => provider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory))
            .Returns(directory);
        fileProvider.Setup(provider => provider.DirectoryExists(directory)).Returns(true);
        fileProvider.Setup(provider => provider.GetFiles(directory, "sitemap-*.xml", true))
            .Returns(files);
        fileProvider.Setup(provider => provider.GetFileName(It.IsAny<string>()))
            .Returns((string path) => Path.GetFileName(path));

        new SitemapArtifactInvalidator(fileProvider.Object).InvalidateGeneratedXmlFiles();

        fileProvider.Verify(provider => provider.DeleteFile(files[0]), Times.Once);
        fileProvider.Verify(provider => provider.DeleteFile(files[1]), Times.Once);
        fileProvider.Verify(provider => provider.DeleteFile(It.IsAny<string>()), Times.Exactly(2));
    }

    [Test]
    public void DoesNothingWhenSitemapDirectoryDoesNotExist()
    {
        const string directory = @"C:\site\sitemaps";
        var fileProvider = new Mock<INopFileProvider>();
        fileProvider.Setup(provider => provider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory))
            .Returns(directory);
        fileProvider.Setup(provider => provider.DirectoryExists(directory)).Returns(false);

        new SitemapArtifactInvalidator(fileProvider.Object).InvalidateGeneratedXmlFiles();

        fileProvider.Verify(provider => provider.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
        fileProvider.Verify(provider => provider.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void ReportsTheExactGeneratedArtifactWhenDeletionFails()
    {
        const string directory = @"C:\site\sitemaps";
        const string filePath = @"C:\site\sitemaps\sitemap-1-24-0.xml";
        var fileProvider = new Mock<INopFileProvider>();
        fileProvider.Setup(provider => provider.GetAbsolutePath(NopSeoDefaults.SitemapXmlDirectory))
            .Returns(directory);
        fileProvider.Setup(provider => provider.DirectoryExists(directory)).Returns(true);
        fileProvider.Setup(provider => provider.GetFiles(directory, "sitemap-*.xml", true))
            .Returns(new[] { filePath });
        fileProvider.Setup(provider => provider.GetFileName(filePath)).Returns(Path.GetFileName(filePath));
        fileProvider.Setup(provider => provider.DeleteFile(filePath))
            .Throws(new UnauthorizedAccessException("denied"));

        var action = () => new SitemapArtifactInvalidator(fileProvider.Object).InvalidateGeneratedXmlFiles();

        action.Should().Throw<IOException>()
            .WithMessage($"*{filePath}*")
            .WithInnerException<UnauthorizedAccessException>();
    }
}
