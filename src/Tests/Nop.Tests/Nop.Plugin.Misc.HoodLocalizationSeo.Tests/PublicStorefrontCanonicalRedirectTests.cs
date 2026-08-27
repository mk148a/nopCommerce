using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Services.Stores;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class PublicStorefrontCanonicalRedirectTests
{
    [TestCase("GET")]
    [TestCase("HEAD")]
    public async Task WwwProductRedirectsToConfiguredCanonicalOrigin(string method)
    {
        var stores = OneStore("https://hoodarcheryshop.com/");
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, method, "/en/ottoman-horse-bow",
            "www.hoodarcheryshop.com", 47177);

        await BuildPipeline(provider)(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status301MovedPermanently));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
                "https://hoodarcheryshop.com/en/ottoman-horse-bow"));
        });
    }

    [TestCase("GET", "/sitemap.xml")]
    [TestCase("HEAD", "/sitemap.xml")]
    [TestCase("GET", "/sitemap-5.xml")]
    [TestCase("HEAD", "/sitemap-5.xml")]
    [TestCase("GET", "/video-sitemap.xml")]
    [TestCase("HEAD", "/video-sitemap.xml")]
    [TestCase("GET", "/en/watch/1/jveOg8xNPyc")]
    [TestCase("HEAD", "/en/watch/1/jveOg8xNPyc")]
    [TestCase("GET", "/robots.txt")]
    [TestCase("HEAD", "/robots.txt")]
    public async Task WwwDiscoveryRoutesRedirectInsteadOfReachingEndpoint(string method, string path)
    {
        var stores = OneStore("https://hoodarcheryshop.com/");
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, method, path, "www.hoodarcheryshop.com");

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            $"https://hoodarcheryshop.com{path}"));
    }

    [Test]
    public async Task PublicCatalogQueryIsPreservedWithoutReflectingRequestAuthority()
    {
        var stores = OneStore("https://hoodarcheryshop.com/");
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, HttpMethods.Get, "/en/search", "www.hoodarcheryshop.com", 4444);
        context.Request.QueryString = new QueryString(
            "?q=horse%20bow&page=2&orderby=10&utm_source=google&returnUrl=%2Fen%2Fcart");

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            "https://hoodarcheryshop.com/en/search?q=horse%20bow&page=2&orderby=10&utm_source=google&returnUrl=%2Fen%2Fcart"));
    }

    [Test]
    public async Task CanonicalApexPassesThroughWithoutStoreLookup()
    {
        var storeService = new Mock<IStoreService>(MockBehavior.Strict);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, HttpMethods.Get, "/en/ottoman-horse-bow",
            "hoodarcheryshop.com");

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
        storeService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AttackerAndDuplicateWwwHostsFailClosedWithoutLocation()
    {
        var duplicateStores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" },
            new Store { Url = "https://hoodarcheryshop.com/second/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };
        var storeService = CreateStoreService(duplicateStores);
        using var provider = CreateProvider(storeService.Object);

        var attacker = CreateContext(provider, HttpMethods.Get, "/en/product", "www.attacker.example");
        await BuildPipeline(provider)(attacker);

        var ambiguous = CreateContext(provider, HttpMethods.Get, "/en/product", "www.hoodarcheryshop.com");
        await BuildPipeline(provider)(ambiguous);

        Assert.Multiple(() =>
        {
            Assert.That(attacker.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(attacker.Response.Headers.Location, Is.Empty);
            Assert.That(ambiguous.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(ambiguous.Response.Headers.Location, Is.Empty);
        });
    }

    [Test]
    public async Task StorePathBaseAndPortAreTrustedWhileRequestPortIsDiscarded()
    {
        var stores = OneStore("https://hoodarcheryshop.com:47176/shop/");
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, HttpMethods.Head, "/sitemap.xml",
            "www.hoodarcheryshop.com", 59999);
        context.Request.PathBase = "/shop";

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            "https://hoodarcheryshop.com:47176/shop/sitemap.xml"));
    }

    [TestCase("/Admin/Product/List")]
    [TestCase("/en/login")]
    [TestCase("/checkout/completed")]
    [TestCase("/api/orders")]
    [TestCase("/Plugins/Payments.Stripe/webhook")]
    [TestCase("/.well-known/acme-challenge/token")]
    [TestCase("/healthz")]
    [TestCase("/images/product.jpg")]
    [TestCase("/en/themes/hood/site.css")]
    public async Task SensitiveAndStaticRoutesAreExcluded(string path)
    {
        var storeService = new Mock<IStoreService>(MockBehavior.Strict);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, HttpMethods.Get, path, "www.hoodarcheryshop.com");

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
        storeService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task NonGetAndRegisteredNonWwwAliasPassThroughOrFailWithoutRedirect()
    {
        var stores = new[]
        {
            new Store { Url = "https://archery-a.example/", Hosts = "archery-a.example" },
            new Store { Url = "https://archery-b.example/", Hosts = "archery-b.example,www.archery-a.example" }
        };
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);

        var post = CreateContext(provider, HttpMethods.Post, "/en/product", "www.archery-a.example");
        await BuildPipeline(provider)(post);

        var otherAlias = CreateContext(provider, HttpMethods.Get, "/en/product", "www.archery-a.example");
        await BuildPipeline(provider)(otherAlias);

        Assert.Multiple(() =>
        {
            Assert.That(post.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(otherAlias.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(otherAlias.Response.Headers.Location, Is.Empty);
        });
    }

    [Test]
    public async Task CanonicalStoreUrlAlreadyUsingWwwPassesThrough()
    {
        var stores = OneStore("https://www.hoodarcheryshop.com/");
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var context = CreateContext(provider, HttpMethods.Get, "/en/product", "www.hoodarcheryshop.com");

        await BuildPipeline(provider)(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
    }

    private static Store[] OneStore(string url) =>
    [
        new Store { Url = url, Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
    ];

    private static DefaultHttpContext CreateContext(IServiceProvider provider, string method,
        string path, string host, int? port = null)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = port.HasValue ? new HostString(host, port.Value) : new HostString(host);
        return context;
    }

    private static ServiceProvider CreateProvider(IStoreService storeService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(storeService);
        return services.BuildServiceProvider();
    }

    private static Mock<IStoreService> CreateStoreService(IList<Store> stores)
    {
        var service = new Mock<IStoreService>(MockBehavior.Strict);
        service.Setup(item => item.GetAllStoresAsync()).ReturnsAsync(stores);
        service.Setup(item => item.ContainsHostValue(It.IsAny<Store>(), It.IsAny<string>()))
            .Returns((Store store, string host) => (store.Hosts ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(candidate => candidate.Trim().Equals(host, StringComparison.OrdinalIgnoreCase)));
        return service;
    }

    private static RequestDelegate BuildPipeline(IServiceProvider provider)
    {
        var application = new ApplicationBuilder(provider);
        new PublicStorefrontCanonicalRedirectNopStartup().Configure(application);
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        return application.Build();
    }
}
