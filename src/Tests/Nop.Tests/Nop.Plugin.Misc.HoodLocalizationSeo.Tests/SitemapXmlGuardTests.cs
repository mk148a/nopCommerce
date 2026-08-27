using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Controllers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Factories;
using Nop.Web.Models.Sitemap;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapXmlGuardTests
{
    private const int StoreId = 3;
    private const int LanguageId = 7;
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const string ValidUrlSet = "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc></url></urlset>";
    private string _testDirectory;
    private string _sitemapDirectory;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Directory.CreateTempSubdirectory("hood-sitemap-guard-").FullName;
        _sitemapDirectory = Path.Combine(_testDirectory, NopSeoDefaults.SitemapXmlDirectory);
        Directory.CreateDirectory(_sitemapDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Test]
    public async Task UrlSetIdOneStreamsExactValidatedCanonicalRootWithoutFactoryOrDuplicate()
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);

        var result = await controller.SitemapXml(1);

        Assert.That(await ReadFileResultAsync(result), Is.EqualTo(ValidUrlSet));
        Assert.That(File.Exists(GetExpectedPath(1)), Is.False);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task StageConfiguredPortUsesTheApprovedPublicOriginForHreflangTransformation()
    {
        const string source = "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"><url><loc>https://hoodarcheryshop.com/en/longbow</loc><xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/en/longbow\" /></url></urlset>";
        var factory = FactoryForRoot(GetExpectedPath(0), () => File.WriteAllText(GetExpectedPath(0), source));
        var controller = CreateController(factory.Object,
            storeUrl: "https://hoodarcheryshop.com:47176/",
            activeStoreLocation: "https://hoodarcheryshop.com/");

        var result = await controller.SitemapXml();

        Assert.That(await ReadFileResultAsync(result), Does.Contain("hreflang=\"en-US\" href=\"https://hoodarcheryshop.com/en/longbow\""));
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(0), Times.Once);
    }

    [Test]
    public async Task UrlSetStalePartReturnsFetchableCanonicalAliasWithoutTrustingRequestHost()
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Host = new HostString("attacker.example");

        var document = await ReadContentXmlAsync(await controller.SitemapXml(5));

        XNamespace sitemap = SitemapNamespace;
        Assert.That(document.Root?.Name, Is.EqualTo(sitemap + "sitemapindex"));
        Assert.That(document.Descendants(sitemap + "loc").Single().Value,
            Is.EqualTo("https://hoodarcheryshop.com/sitemap.xml"));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task StorePathBaseIsPreservedInCanonicalAlias()
    {
        await WriteRootAsync(ValidUrlSet.Replace("https://hoodarcheryshop.com/",
            "https://hoodarcheryshop.com/shop/", StringComparison.Ordinal));
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object,
            storeUrl: "https://hoodarcheryshop.com/shop/");

        var document = await ReadContentXmlAsync(await controller.SitemapXml(5));

        XNamespace sitemap = SitemapNamespace;
        Assert.That(document.Descendants(sitemap + "loc").Single().Value,
            Is.EqualTo("https://hoodarcheryshop.com/shop/sitemap.xml"));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UnsafeStoreUrlFailsClosedWithoutInvokingFactory()
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object, storeUrl: "javascript://attacker.example/");

        AssertRetryable503(await controller.SitemapXml(5), controller);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UnsafeStoreUrlFailsClosedForCanonicalIdOneLkgWithoutInvokingFactory()
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object, storeUrl: "javascript://attacker.example/");

        AssertRetryable503(await controller.SitemapXml(1), controller);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SitemapIndexUnlistedPartStreamsExactValidatedRootIndex()
    {
        var root = SitemapIndex("https://hoodarcheryshop.com/sitemap-1.xml");
        await WriteRootAsync(root);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);

        Assert.That(await ReadFileResultAsync(await controller.SitemapXml(5)), Is.EqualTo(root));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task PathBasedSitemapIndexLocationIsAcceptedOnlyUnderStoreBase()
    {
        var root = SitemapIndex("https://hoodarcheryshop.com/shop/sitemap-2.xml");
        await WriteRootAsync(root);
        var expectedPath = GetExpectedPath(2);
        var pathBasedUrlSet = ValidUrlSet.Replace("https://hoodarcheryshop.com/",
            "https://hoodarcheryshop.com/shop/", StringComparison.Ordinal);
        var factory = FactoryForPart(2, expectedPath, () => File.WriteAllText(expectedPath, pathBasedUrlSet));
        var controller = CreateController(factory.Object,
            storeUrl: "https://hoodarcheryshop.com/shop/");

        Assert.That(await ReadFileResultAsync(await controller.SitemapXml(2)), Is.EqualTo(pathBasedUrlSet));
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(2), Times.Once);
    }

    [Test]
    public async Task ColdNumberedRequestBootstrapsOnlyCanonicalIdZeroThenReevaluatesTopology()
    {
        var rootPath = GetExpectedPath(0);
        var factory = FactoryForRoot(rootPath, () => File.WriteAllText(rootPath, ValidUrlSet));
        var controller = CreateController(factory.Object);

        Assert.That(await ReadFileResultAsync(await controller.SitemapXml(1)), Is.EqualTo(ValidUrlSet));
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(0), Times.Once);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ColdStalePartBootstrapsOnlyCanonicalIdZeroThenReturnsAlias()
    {
        var rootPath = GetExpectedPath(0);
        var factory = FactoryForRoot(rootPath, () => File.WriteAllText(rootPath, ValidUrlSet));
        var controller = CreateController(factory.Object);

        var document = await ReadContentXmlAsync(await controller.SitemapXml(5));

        Assert.That(document.Root?.Name.LocalName, Is.EqualTo("sitemapindex"));
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(0), Times.Once);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CanonicalLockContentionServesOnlyProtocolValidPartAsLkg()
    {
        var expectedPath = GetExpectedPath(2);
        await File.WriteAllTextAsync(expectedPath, ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(0))
            .ThrowsAsync(new InvalidOperationException());
        var controller = CreateController(factory.Object);

        Assert.That(await ReadFileResultAsync(await controller.SitemapXml(2)), Is.EqualTo(ValidUrlSet));
    }

    [Test]
    public async Task CanonicalLockWithoutValidLkgReturnsRetryable503()
    {
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(0))
            .ThrowsAsync(new InvalidOperationException());
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(2), controller);
    }

    [TestCase("<urlset />")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url /></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc /></url></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><wrapper><url><loc>https://hoodarcheryshop.com/</loc></url></wrapper></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc><extra /></url></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc><lastmod>2026-08-14</lastmod><lastmod>2026-08-13</lastmod></url></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc><changefreq>daily</changefreq><changefreq>weekly</changefreq></url></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc><priority>0.8</priority><priority>0.5</priority></url></urlset>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">")]
    [TestCase("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap /></sitemapindex>")]
    [TestCase("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><wrapper><sitemap><loc>https://hoodarcheryshop.com/sitemap-2.xml</loc></sitemap></wrapper></sitemapindex>")]
    [TestCase("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap><loc>https://hoodarcheryshop.com/sitemap-2.xml</loc><extra /></sitemap></sitemapindex>")]
    [TestCase("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap><loc>https://hoodarcheryshop.com/sitemap-2.xml</loc><lastmod>2026-08-14</lastmod><lastmod>2026-08-13</lastmod></sitemap></sitemapindex>")]
    [TestCase("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://hoodarcheryshop.com/</loc></url></urlset>garbage")]
    public async Task InvalidPartIsNeverServedAs200(string invalidXml)
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var expectedPath = GetExpectedPath(2);
        var factory = FactoryForPart(2, expectedPath, () => File.WriteAllText(expectedPath, invalidXml));
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(2), controller);
    }

    [Test]
    public async Task CoreShapedSitemapSiblingsRemainValid()
    {
        const string urlSet = "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"><url><loc>https://hoodarcheryshop.com/</loc><xhtml:link rel=\"alternate\" hreflang=\"tr\" href=\"https://hoodarcheryshop.com/tr/\" /><xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"https://hoodarcheryshop.com/en/\" /><changefreq>daily</changefreq><lastmod>2026-08-14</lastmod><priority>0.8</priority></url></urlset>";
        await WriteRootAsync(urlSet);
        var controller = CreateController(new Mock<ISitemapModelFactory>(MockBehavior.Strict).Object);
        var transformed = XDocument.Parse(await ReadFileResultAsync(await controller.SitemapXml(1)));
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        Assert.That(transformed.Descendants(xhtml + "link")
            .Select(link => (link.Attribute("hreflang")?.Value, link.Attribute("href")?.Value)),
            Is.EqualTo(new[]
            {
                ("tr-TR", "https://hoodarcheryshop.com/tr/"),
                ("en-US", "https://hoodarcheryshop.com/en/"),
                ("x-default", "https://hoodarcheryshop.com/en/")
            }));
        Assert.That(await File.ReadAllTextAsync(GetExpectedPath(0)), Is.EqualTo(urlSet),
            "The cached core artifact must remain byte-for-byte unchanged.");

        var index = SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml")
            .Replace("</sitemap>", "<lastmod>2026-08-14</lastmod></sitemap>", StringComparison.Ordinal);
        await WriteRootAsync(index);
        Assert.That(await ReadFileResultAsync(await controller.SitemapXml(5)), Is.EqualTo(index));
    }

    [Test]
    public async Task OversizedPartIsNeverServedAs200()
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var expectedPath = GetExpectedPath(2);
        var factory = FactoryForPart(2, expectedPath, () =>
        {
            using var stream = new FileStream(expectedPath, FileMode.Create, FileAccess.Write);
            stream.SetLength(52_428_801);
        });
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(2), controller);
    }

    [Test]
    public async Task SitemapWithMoreThanFiftyThousandUrlEntriesIsNeverServedAs200()
    {
        var rootPath = GetExpectedPath(0);
        var oversizedUrlSet = $"<urlset xmlns=\"{SitemapNamespace}\">" +
            string.Concat(Enumerable.Repeat("<url />", 50_001)) + "</urlset>";
        await WriteRootAsync(oversizedUrlSet);
        var factory = FactoryForRoot(rootPath);
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(1), controller);
    }

    [Test]
    public async Task CrossOriginIndexLocationCannotDriveRouting()
    {
        var rootPath = GetExpectedPath(0);
        await WriteRootAsync(SitemapIndex("https://attacker.example/sitemap-1.xml"));
        var factory = FactoryForRoot(rootPath);
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(5), controller);
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(0), Times.Once);
    }

    [Test]
    public async Task LocationOutsideStorePathBaseCannotDriveRouting()
    {
        var rootPath = GetExpectedPath(0);
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var factory = FactoryForRoot(rootPath);
        var controller = CreateController(factory.Object,
            storeUrl: "https://hoodarcheryshop.com/shop/");

        AssertRetryable503(await controller.SitemapXml(2), controller);
    }

    [Test]
    public async Task ForeignUrlSetLocationFailsClosedForRootAndNumberedLkg()
    {
        const string foreignUrlSet = "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://attacker.example/product</loc></url></urlset>";
        var rootPath = GetExpectedPath(0);
        await WriteRootAsync(foreignUrlSet);
        var rootFactory = FactoryForRoot(rootPath);
        var rootController = CreateController(rootFactory.Object);
        AssertRetryable503(await rootController.SitemapXml(), rootController);

        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var partPath = GetExpectedPath(2);
        await File.WriteAllTextAsync(partPath, foreignUrlSet);
        var partController = CreateController(FactoryForPart(2, partPath).Object);
        AssertRetryable503(await partController.SitemapXml(2), partController);
    }

    [Test]
    public async Task RootWithTrailingGarbageFailsClosed()
    {
        var rootPath = GetExpectedPath(0);
        await WriteRootAsync(ValidUrlSet + "garbage");
        var factory = FactoryForRoot(rootPath);
        var controller = CreateController(factory.Object);

        AssertRetryable503(await controller.SitemapXml(), controller);
    }

    [Test]
    public async Task PathOutsideExactSitemapDirectoryIsRejected()
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var outsidePath = Path.Combine(_testDirectory, $"sitemap-{StoreId}-{LanguageId}-2.xml");
        await File.WriteAllTextAsync(outsidePath, ValidUrlSet);
        var factory = FactoryForPart(2, outsidePath);
        var controller = CreateController(factory.Object, outsidePath);

        AssertRetryable503(await controller.SitemapXml(2), controller);
    }

    [Test]
    public void UnexpectedCanonicalFactoryExceptionIsNotSwallowed()
    {
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(0))
            .ThrowsAsync(new ApplicationException("unexpected"));
        var controller = CreateController(factory.Object);

        Assert.ThrowsAsync<ApplicationException>(async () => await controller.SitemapXml(1));
    }

    [Test]
    public async Task UnexpectedPartFactoryExceptionIsNotSwallowed()
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(2))
            .ThrowsAsync(new ApplicationException("unexpected"));
        var controller = CreateController(factory.Object);

        Assert.ThrowsAsync<ApplicationException>(async () => await controller.SitemapXml(2));
    }

    [Test]
    public async Task DisabledSitemapPreservesCoreForbiddenBehavior()
    {
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object, sitemapXmlEnabled: false);

        var result = await controller.SitemapXml(1);

        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public void NumberedSitemapRouteIsStrictAndOverridesCorePriority()
    {
        var provider = new HoodRouteProvider();
        var coreProvider = new Nop.Web.Infrastructure.RouteProvider();

        Assert.Multiple(() =>
        {
            Assert.That(HoodRouteProvider.IndexedSitemapRoutePattern,
                Is.EqualTo("sitemap-{id:int:min(1)}.xml"));
            Assert.That(HoodRouteProvider.RootSitemapRoutePattern, Is.EqualTo("sitemap.xml"));
            Assert.That(provider.Priority, Is.GreaterThan(coreProvider.Priority));
        });
    }

    [Test]
    public void SitemapRoutesRegisterExpectedControllerEndpoints()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton(new DiagnosticListener("HoodSitemapRouteTests"));
        services.AddControllers().AddApplicationPart(typeof(HoodLocalizationController).Assembly);
        using var serviceProvider = services.BuildServiceProvider();
        var routeBuilder = new TestEndpointRouteBuilder(serviceProvider);

        HoodRouteProvider.RegisterSitemapRoutes(routeBuilder);

        var endpoints = routeBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName is
                "HoodSitemapXml" or "HoodIndexedSitemapXml" &&
                endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .OrderBy(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.That(endpoints, Has.Length.EqualTo(2), string.Join(Environment.NewLine,
            endpoints.Select(endpoint => $"{endpoint.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName}: " +
                $"{endpoint.RoutePattern.RawText} -> " +
                $"{endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.DisplayName}")));
        Assert.Multiple(() =>
        {
            Assert.That(endpoints.Select(endpoint => endpoint.RoutePattern.RawText), Is.EquivalentTo(new[]
            {
                HoodRouteProvider.RootSitemapRoutePattern,
                HoodRouteProvider.IndexedSitemapRoutePattern
            }));
            Assert.That(endpoints.Select(endpoint => endpoint.Metadata
                    .GetMetadata<ControllerActionDescriptor>()?.ControllerName),
                Is.All.EqualTo("HoodLocalization"));
            Assert.That(endpoints.Select(endpoint => endpoint.Metadata
                    .GetMetadata<ControllerActionDescriptor>()?.ActionName),
                Is.All.EqualTo("SitemapXml"));
            Assert.That(endpoints.Select(endpoint => endpoint.Metadata
                    .GetMetadata<IHttpMethodMetadata>()?.HttpMethods),
                Is.All.EquivalentTo(new[] { HttpMethods.Get, HttpMethods.Head }));
            Assert.That(endpoints.Single(endpoint => endpoint.Metadata
                    .GetMetadata<IRouteNameMetadata>()?.RouteName == "HoodSitemapXml")
                    .RoutePattern.Defaults["id"],
                Is.EqualTo(0));
        });
    }

    [TestCase(0)]
    [TestCase(5)]
    public async Task SitemapHeadReturnsRetryable503WhenCanonicalRootIsAbsentWithoutCallingTheFactory(int id)
    {
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(id);

        AssertRetryable503(result, controller);
        factory.VerifyNoOtherCalls();
    }

    [TestCase(0)]
    [TestCase(5)]
    public async Task SitemapHeadReturnsRetryable503WhenCanonicalRootIsEmptyWithoutCallingTheFactory(int id)
    {
        await File.WriteAllBytesAsync(GetExpectedPath(0), Array.Empty<byte>());
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(id);

        AssertRetryable503(result, controller);
        factory.VerifyNoOtherCalls();
    }

    [TestCase(0)]
    [TestCase(5)]
    public async Task SitemapHeadReturnsXmlHeadersForPresentCanonicalRootWithoutCallingTheFactory(int id)
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<EmptyResult>());
            Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(controller.Response.ContentType, Is.EqualTo(MimeTypes.ApplicationXml));
            Assert.That(controller.Response.Headers.Link.ToString(), Is.Empty);
        });
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SitemapHeadUrlSetAliasReturns200WithoutCallingTheFactory()
    {
        await WriteRootAsync(ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(5);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SitemapHeadSitemapIndexUnlistedPartReturns200WithoutCallingTheFactory()
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(5);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        factory.VerifyNoOtherCalls();
    }

    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("oversized")]
    public async Task SitemapHeadListedPartReturnsRetryable503WithoutCallingTheFactory(string partState)
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        var partPath = GetExpectedPath(2);
        switch (partState)
        {
            case "empty":
                await File.WriteAllBytesAsync(partPath, Array.Empty<byte>());
                break;
            case "oversized":
                await using (var stream = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write))
                    stream.SetLength(50L * 1024 * 1024 + 1);
                break;
        }

        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(2);

        AssertRetryable503(result, controller);
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task SitemapHeadPresentListedPartReturns200WithoutCallingTheFactory()
    {
        await WriteRootAsync(SitemapIndex("https://hoodarcheryshop.com/sitemap-2.xml"));
        await File.WriteAllTextAsync(GetExpectedPath(2), ValidUrlSet);
        var factory = new Mock<ISitemapModelFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);
        controller.Request.Method = HttpMethods.Head;

        var result = await controller.SitemapXml(2);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        factory.VerifyNoOtherCalls();
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IApplicationBuilder CreateApplicationBuilder()
        {
            return new ApplicationBuilder(ServiceProvider);
        }

        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        public IServiceProvider ServiceProvider { get; } = serviceProvider;
    }

    [Test]
    public async Task RootSitemapRouteUsesCoreFactoryThenServesValidatedArtifact()
    {
        var rootPath = GetExpectedPath(0);
        var factory = FactoryForRoot(rootPath, () => File.WriteAllText(rootPath, ValidUrlSet));
        var controller = CreateController(factory.Object);

        Assert.That(await ReadFileResultAsync(await controller.SitemapXml()), Is.EqualTo(ValidUrlSet));
        factory.Verify(item => item.PrepareSitemapXmlModelAsync(0), Times.Once);
    }

    private static string SitemapIndex(params string[] locations)
    {
        XNamespace sitemap = SitemapNamespace;
        return new XDocument(new XElement(sitemap + "sitemapindex",
            locations.Select(location => new XElement(sitemap + "sitemap",
                new XElement(sitemap + "loc", location)))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private string GetExpectedPath(int id) =>
        Path.Combine(_sitemapDirectory, $"sitemap-{StoreId}-{LanguageId}-{id}.xml");

    private Task WriteRootAsync(string xml) => File.WriteAllTextAsync(GetExpectedPath(0), xml);

    private Mock<ISitemapModelFactory> FactoryForRoot(string path, Action write = null)
    {
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(0)).ReturnsAsync(() =>
        {
            write?.Invoke();
            return new SitemapXmlModel { SitemapXmlPath = path };
        });
        return factory;
    }

    private Mock<ISitemapModelFactory> FactoryForPart(int id, string path, Action write = null)
    {
        var factory = new Mock<ISitemapModelFactory>();
        factory.Setup(item => item.PrepareSitemapXmlModelAsync(id)).ReturnsAsync(() =>
        {
            write?.Invoke();
            return new SitemapXmlModel { SitemapXmlPath = path };
        });
        return factory;
    }

    private HoodLocalizationController CreateController(ISitemapModelFactory factory,
        string expectedPathOverride = null,
        bool sitemapXmlEnabled = true,
        string storeUrl = "https://hoodarcheryshop.com/",
        string activeStoreLocation = null)
    {
        var fileProvider = new Mock<INopFileProvider>();
        fileProvider.Setup(provider => provider.GetAbsolutePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => paths.Length == 1
                ? _sitemapDirectory
                : expectedPathOverride ?? Path.Combine(_sitemapDirectory, paths[^1]));

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync())
            .ReturnsAsync(new Store { Id = StoreId, DefaultLanguageId = 1, Url = storeUrl });
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetWorkingLanguageAsync())
            .ReturnsAsync(new Language { Id = LanguageId });
        var languageService = new Mock<ILanguageService>();
        languageService.Setup(service => service.GetAllLanguagesAsync(false, StoreId))
            .ReturnsAsync(new[]
            {
                new Language
                {
                    Id = 1, UniqueSeoCode = "en", LanguageCulture = "en-US", Published = true
                },
                new Language
                {
                    Id = LanguageId, UniqueSeoCode = "tr", LanguageCulture = "tr-TR", Published = true
                }
            });
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns(activeStoreLocation ?? storeUrl);

        return new HoodLocalizationController(
            new BlogSettings(),
            new SitemapXmlSettings
            {
                SitemapXmlEnabled = sitemapXmlEnabled,
                SitemapBuildOperationDelay = 60
            },
            Mock.Of<IBlogLocalizationService>(),
            Mock.Of<IBlogService>(),
            Mock.Of<ICommonModelFactory>(),
            languageService.Object,
            Mock.Of<ILocalizationService>(),
            fileProvider.Object,
            new SitemapHreflangStreamTransformer(),
            factory,
            storeContext.Object,
            Mock.Of<ITopicService>(),
            Mock.Of<IUrlRecordService>(),
            webHelper.Object,
            workContext.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static async Task<string> ReadFileResultAsync(IActionResult result)
    {
        Assert.That(result, Is.TypeOf<FileStreamResult>());
        var fileResult = (FileStreamResult)result;
        await using var stream = fileResult.FileStream;
        using var reader = new StreamReader(stream);
        Assert.That(fileResult.ContentType, Is.EqualTo(MimeTypes.ApplicationXml));
        return await reader.ReadToEndAsync();
    }

    private static Task<XDocument> ReadContentXmlAsync(IActionResult result)
    {
        Assert.That(result, Is.TypeOf<ContentResult>());
        var content = (ContentResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(content.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(content.ContentType, Is.EqualTo("application/xml; charset=utf-8"));
        });
        return Task.FromResult(XDocument.Parse(content.Content));
    }

    private static void AssertRetryable503(IActionResult result, ControllerBase controller)
    {
        Assert.That(result, Is.TypeOf<StatusCodeResult>());
        Assert.Multiple(() =>
        {
            Assert.That(((StatusCodeResult)result).StatusCode,
                Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
            Assert.That(controller.Response.Headers.RetryAfter.ToString(), Is.EqualTo("60"));
        });
    }

}
