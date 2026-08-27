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
public sealed class LegacyAuWoodenArrowsRedirectTests
{
    [TestCase("GET", "/au/wooden-arrows")]
    [TestCase("HEAD", "/au/wooden-arrows/")]
    public async Task ExactLegacyGetAndHeadRedirectPermanently(string method, string path)
    {
        var context = CreateContext(method, path);

        await BuildPipeline()(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status301MovedPermanently));
            Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
                LegacyAuWoodenArrowsRedirect.TargetPath));
        });
    }

    [Test]
    public async Task OnlySafeAttributionParametersArePreserved()
    {
        var context = CreateContext(HttpMethods.Get, LegacyAuWoodenArrowsRedirect.LegacyPath);
        context.Request.QueryString = new QueryString(
            "?gclid=abc%20123&utm_source=google&utm_campaign=au-bows&returnUrl=%2Fcart&coupon=SAVE7&q=wooden%20arrows");

        await BuildPipeline()(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            "/au/wooden-arrows-2?gclid=abc%20123&utm_source=google&utm_campaign=au-bows"));
    }

    [Test]
    public async Task PreservesPathBaseWithoutReflectingHostOrUnsafeQuery()
    {
        var context = CreateContext(HttpMethods.Head, LegacyAuWoodenArrowsRedirect.LegacyPath);
        context.Request.PathBase = "/shop";
        context.Request.QueryString = new QueryString("?wbraid=w123&returnUrl=https%3A%2F%2Fattacker.example");

        await BuildPipeline()(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            "/shop/au/wooden-arrows-2?wbraid=w123"));
    }

    [TestCase("POST", "/au/wooden-arrows")]
    [TestCase("PUT", "/au/wooden-arrows")]
    [TestCase("GET", "/AU/wooden-arrows")]
    [TestCase("GET", "/au/wooden-arrows/extra")]
    [TestCase("GET", "/au/wooden-arrows-2")]
    [TestCase("GET", "/en/wooden-arrows")]
    [TestCase("GET", "/content/au/wooden-arrows.css")]
    public async Task NonExactOrNonSafeRequestsPassThrough(string method, string path)
    {
        var context = CreateContext(method, path);

        await BuildPipeline()(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(context.Response.Headers.Location, Is.Empty);
        });
    }

    [Test]
    public void StartupRunsAfterHostCanonicalizationAndBeforeEndpoints()
    {
        var startup = new LegacyAuWoodenArrowsRedirectNopStartup();

        Assert.Multiple(() =>
        {
            Assert.That(startup.Order, Is.GreaterThan(new PublicStorefrontCanonicalRedirectNopStartup().Order));
            Assert.That(startup.Order, Is.GreaterThan(new ProductContactUsTabNoIndexNopStartup().Order));
            Assert.That(startup.Order, Is.LessThan(new Nop.Web.Framework.Infrastructure.NopEndpoints().Order));
        });
    }

    [Test]
    public async Task WwwAliasIsCanonicalizedBeforeTheLegacyPathRedirect()
    {
        var store = new Store
        {
            Url = "https://hoodarcheryshop.com/",
            Hosts = "hoodarcheryshop.com,www.hoodarcheryshop.com"
        };
        var storeService = new Mock<IStoreService>(MockBehavior.Strict);
        storeService.Setup(item => item.GetAllStoresAsync()).ReturnsAsync([store]);
        storeService.Setup(item => item.ContainsHostValue(store, "www.hoodarcheryshop.com"))
            .Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(storeService.Object);
        using var provider = services.BuildServiceProvider();
        var application = new ApplicationBuilder(provider);
        new PublicStorefrontCanonicalRedirectNopStartup().Configure(application);
        new LegacyAuWoodenArrowsRedirectNopStartup().Configure(application);
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var context = CreateContext(HttpMethods.Get, LegacyAuWoodenArrowsRedirect.LegacyPath);
        context.RequestServices = provider;
        context.Request.Host = new HostString("www.hoodarcheryshop.com");
        context.Request.QueryString = new QueryString("?gclid=abc&returnUrl=%2Fcart");

        await application.Build()(context);

        Assert.That(context.Response.Headers.Location.ToString(), Is.EqualTo(
            "https://hoodarcheryshop.com/au/wooden-arrows?gclid=abc&returnUrl=%2Fcart"));
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private static RequestDelegate BuildPipeline()
    {
        var application = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        new LegacyAuWoodenArrowsRedirectNopStartup().Configure(application);
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        return application.Build();
    }
}
