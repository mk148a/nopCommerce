using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Core;
using Nop.Plugin.Widgets.HoodVideoSeo.Controllers;
using Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Tests;

[TestFixture]
public sealed class VideoSitemapDiscoveryTests
{
    [Test]
    public void RouteProviderExposesCanonicalRootXmlEndpoint()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers();
        using var app = builder.Build();
        var routeBuilder = (IEndpointRouteBuilder)app;

        new VideoWatchRouteProvider().RegisterRoutes(routeBuilder);

        var patterns = routeBuilder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();
        Assert.That(patterns, Does.Contain("video-sitemap.xml"));
    }

    [Test]
    public void CanonicalRootXmlActionExplicitlyAcceptsGetAndHead()
    {
        var action = typeof(VideoWatchController).GetMethod(nameof(VideoWatchController.ProductVideos));
        var verbs = action?.GetCustomAttributes(typeof(AcceptVerbsAttribute), inherit: true)
            .Cast<AcceptVerbsAttribute>()
            .Single()
            .HttpMethods;

        Assert.That(verbs, Is.EquivalentTo(new[] { HttpMethods.Get, HttpMethods.Head }));
    }

    [Test]
    public async Task RobotsResultGetsOneCanonicalVideoSitemapDirective()
    {
        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(helper => helper.GetStoreLocation(null)).Returns("https://shop.example/");
        var filter = new VideoSitemapDiscoveryResultFilter(webHelper.Object);
        var descriptor = new ActionDescriptor
        {
            RouteValues = new Dictionary<string, string>
            {
                ["controller"] = "Common",
                ["action"] = "RobotsTextFile"
            }
        };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
        var content = new ContentResult { Content = "User-agent: *\r\nSitemap: https://shop.example/sitemap.xml\r\n" };

        async Task ExecuteAsync()
        {
            var executing = new ResultExecutingContext(actionContext, new List<IFilterMetadata>(), content, new object());
            await filter.OnResultExecutionAsync(executing, () => Task.FromResult(
                new ResultExecutedContext(actionContext, new List<IFilterMetadata>(), content, new object())));
        }

        await ExecuteAsync();
        await ExecuteAsync();

        Assert.That(content.Content?.Split("Sitemap: https://shop.example/video-sitemap.xml").Length - 1, Is.EqualTo(1));
    }

}
