using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using Nop.Services.Stores;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class HalloweenLandingCanonicalRedirectTests
{
    [Test]
    public void TrustedWwwAliasBuildsCanonicalLandingAndRetainsTrackingOnly()
    {
        var stores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/shop/", Hosts = "hoodarcheryshop.com, www.hoodarcheryshop.com" }
        };

        var storeService = CreateStoreService(stores);
        var found = HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService.Object, stores,
            new HostString("www.hoodarcheryshop.com", 47175), out var origin, out var hostKind);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?utm_source=newsletter&gclid=123&returnUrl=%2Fadmin");

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(hostKind, Is.EqualTo(CanonicalHostKind.WwwAlias));
            Assert.That(HalloweenLandingCanonicalRedirect.BuildCanonicalUrl(origin, "en"), Is.EqualTo(
                "https://hoodarcheryshop.com/shop/en/halloween-archery-and-costume-guide"));
            Assert.That(HalloweenLandingCanonicalRedirect.AppendAllowedTrackingQuery(
                HalloweenLandingCanonicalRedirect.BuildCanonicalUrl(origin, "en"), context.Request.Query), Is.EqualTo(
                "https://hoodarcheryshop.com/shop/en/halloween-archery-and-costume-guide?utm_source=newsletter&gclid=123"));
        });
    }

    [Test]
    public void UnknownHostAndNonLandingRoutesAreRejected()
    {
        var stores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };

        var storeService = CreateStoreService(stores);
        Assert.Multiple(() =>
        {
            Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService.Object, stores,
                new HostString("www.attacker.example"), out _, out _), Is.False);
            Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService.Object, stores,
                new HostString("hoodarcheryshop.com"), out _, out var hostKind), Is.True);
            Assert.That(hostKind, Is.EqualTo(CanonicalHostKind.Canonical));
            Assert.That(HalloweenLandingCanonicalRedirect.IsLandingGetOrHead(
                new PathString("/en/halloween-archery-and-costume-guide"), HttpMethods.Post, out _), Is.False);
            Assert.That(HalloweenLandingCanonicalRedirect.IsLandingGetOrHead(
                new PathString("/en/not-the-landing"), HttpMethods.Get, out _), Is.False);
        });
    }

    [TestCase("en")]
    [TestCase("tr")]
    [TestCase("de")]
    [TestCase("fr")]
    [TestCase("es")]
    public void EveryPublishedLandingLocaleIsInMiddlewareScope(string language)
    {
        Assert.That(HalloweenLandingCanonicalRedirect.IsLandingGetOrHead(
            new PathString(HalloweenLandingRoute.BuildPath(language)), HttpMethods.Head, out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(language));
    }

    [Test]
    public async Task ConfiguredMiddlewareRunsBeforeTerminalEndpointAndCanonicalizesWww()
    {
        var stores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com:47176/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var pipeline = BuildPipeline(provider, StatusCodes.Status204NoContent);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = HttpMethods.Head;
        context.Request.Path = HalloweenLandingRoute.BuildPath("en");
        context.Request.Host = new HostString("www.hoodarcheryshop.com", 47177);
        context.Request.QueryString = new QueryString("?utm_source=test&returnUrl=%2Fadmin");

        await pipeline(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status301MovedPermanently));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
                "https://hoodarcheryshop.com:47176/en/halloween-archery-and-costume-guide?utm_source=test"));
        });
        storeService.Verify(service => service.GetAllStoresAsync(), Times.Once);
    }

    [Test]
    public async Task MiddlewarePassesApexToEndpointAndRejectsAttackerHost()
    {
        var stores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var pipeline = BuildPipeline(provider, StatusCodes.Status204NoContent);

        var apex = new DefaultHttpContext { RequestServices = provider };
        apex.Request.Method = HttpMethods.Get;
        apex.Request.Path = HalloweenLandingRoute.BuildPath("tr");
        apex.Request.Host = new HostString("hoodarcheryshop.com");
        await pipeline(apex);

        var attacker = new DefaultHttpContext { RequestServices = provider };
        attacker.Request.Method = HttpMethods.Get;
        attacker.Request.Path = HalloweenLandingRoute.BuildPath("tr");
        attacker.Request.Host = new HostString("www.attacker.example");
        await pipeline(attacker);

        Assert.Multiple(() =>
        {
            Assert.That(apex.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(attacker.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(attacker.Response.Headers.Location, Is.Empty);
        });
    }

    [Test]
    public void AliasMustBelongToTheSameStoreAsTheCanonicalTarget()
    {
        var stores = new[]
        {
            new Store { Url = "https://archery-a.example/", Hosts = "archery-a.example" },
            new Store { Url = "https://archery-b.example/", Hosts = "archery-b.example,www.archery-a.example" }
        };

        var storeService = CreateStoreService(stores);

        Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService.Object, stores,
            new HostString("www.archery-a.example"), out _, out var kind), Is.True);
        Assert.That(kind, Is.EqualTo(CanonicalHostKind.OtherRegisteredAlias));
    }

    [Test]
    public void DuplicateStoreAliasFailsClosedInsteadOfChoosingAStore()
    {
        var stores = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" },
            new Store { Url = "https://hoodarcheryshop.com/second/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };

        var storeService = CreateStoreService(stores);

        Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(storeService.Object, stores,
            new HostString("www.hoodarcheryshop.com"), out _, out _), Is.False);
    }

    [Test]
    public void UrlAlreadyOnWwwPassesThroughAndMissingWwwAliasFailsClosed()
    {
        var canonicalWww = new[]
        {
            new Store { Url = "https://www.hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com" }
        };
        var missingAlias = new[]
        {
            new Store { Url = "https://hoodarcheryshop.com/", Hosts = "hoodarcheryshop.com" }
        };
        var canonicalService = CreateStoreService(canonicalWww);
        var missingAliasService = CreateStoreService(missingAlias);

        Assert.Multiple(() =>
        {
            Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(canonicalService.Object,
                canonicalWww, new HostString("www.hoodarcheryshop.com"), out _, out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(CanonicalHostKind.Canonical));
            Assert.That(HalloweenLandingCanonicalRedirect.TryGetCanonicalStoreForHost(missingAliasService.Object,
                missingAlias, new HostString("www.hoodarcheryshop.com"), out _, out _), Is.False);
        });
    }

    [Test]
    public async Task MiddlewareDoesNotRedirectRegisteredAliasThatIsNotTheStoreWwwAlias()
    {
        var stores = new[]
        {
            new Store { Url = "https://archery-a.example/", Hosts = "archery-a.example" },
            new Store { Url = "https://archery-b.example/", Hosts = "archery-b.example,www.archery-a.example" }
        };
        var storeService = CreateStoreService(stores);
        using var provider = CreateProvider(storeService.Object);
        var pipeline = BuildPipeline(provider, StatusCodes.Status204NoContent);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = HalloweenLandingRoute.BuildPath("en");
        context.Request.Host = new HostString("www.archery-a.example");

        await pipeline(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(context.Response.Headers.Location, Is.Empty);
        });
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

    private static RequestDelegate BuildPipeline(IServiceProvider provider, int terminalStatusCode)
    {
        var application = new ApplicationBuilder(provider);
        new HalloweenCanonicalRedirectNopStartup().Configure(application);
        application.Run(context =>
        {
            context.Response.StatusCode = terminalStatusCode;
            return Task.CompletedTask;
        });
        return application.Build();
    }
}
