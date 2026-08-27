using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class ProductContactUsTabNoIndexTests
{
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/190", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("HEAD", "/en/ProductTab/ProductContactUsTab/190", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("HEAD", "/ProductTab/ProductContactUsTab/190", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("GET", "/EN/producttab/productcontactustab/190/", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("GET", "/en/filterSearch", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/fr/filterSearch/", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/filterSearch/", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/filterSearch", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/recentlyviewedproducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/en/recentlyviewedproducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/fr/compareproducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    public async Task EligibleRouteAddsNoIndexHeaderAndPreservesEndpointResponse(string method, string path,
        string expectedHeaderValue)
    {
        var context = CreateContext(method, path);
        context.Request.QueryString = new QueryString("?utm_source=google&tab=contact");

        await BuildPipeline()(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status202Accepted));
            Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
                Is.EqualTo(expectedHeaderValue));
            Assert.That(context.Request.QueryString.ToString(), Is.EqualTo("?utm_source=google&tab=contact"));
        });
    }

    [TestCase("POST", "/en/ProductTab/ProductContactUsTab/190")]
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/not-a-number")]
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/190/extra")]
    [TestCase("GET", "/en/ProductTab/ProductReviewsTab/190")]
    [TestCase("GET", "/en/wooden-ottoman-hunting-arrows")]
    [TestCase("GET", "/Admin/ProductTab/ProductContactUsTab/190")]
    [TestCase("GET", "/en/filterSearch/extra")]
    [TestCase("GET", "/en/filterSearchResults")]
    [TestCase("GET", "/en/recentlyviewedproducts/190")]
    [TestCase("GET", "/en/compareproducts/190")]
    [TestCase("GET", "/en/producttag/12")]
    [TestCase("GET", "/en/manufacturer/12")]
    [TestCase("GET", "/en/newproducts")]
    [TestCase("GET", "/pt-BR/filterSearch")]
    [TestCase("GET", "/en-US/filterSearch")]
    [TestCase("GET", "/pt-BR/ProductTab/ProductContactUsTab/190")]
    public async Task OtherMethodsAndRoutesDoNotReceiveNoIndexHeader(string method, string path)
    {
        var context = CreateContext(method, path);

        await BuildPipeline()(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status202Accepted));
            Assert.That(context.Response.Headers.ContainsKey(ProductContactUsTabNoIndex.HeaderName), Is.False);
        });
    }

    [Test]
    public void StartedResponseIsNeverModified()
    {
        var context = CreateContext(HttpMethods.Get, "/en/ProductTab/ProductContactUsTab/190");
        var responseFeature = new Mock<IHttpResponseFeature>(MockBehavior.Strict);
        responseFeature.SetupGet(item => item.HasStarted).Returns(true);
        responseFeature.SetupGet(item => item.Headers).Returns(new HeaderDictionary());
        context.Features.Set<IHttpResponseFeature>(responseFeature.Object);

        var applied = ProductContactUsTabNoIndex.TryApply(context);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.False);
            Assert.That(context.Response.Headers.ContainsKey(ProductContactUsTabNoIndex.HeaderName), Is.False);
        });
    }

    [Test]
    public async Task EligibleRouteHeaderWinsOverDownstreamOverwrite()
    {
        var context = CreateContext(HttpMethods.Get, "/en/newproducts");
        context.Request.QueryString = new QueryString("?pagenumber=2");
        var application = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        new ProductContactUsTabNoIndexNopStartup().Configure(application);
        application.Run(nextContext =>
        {
            nextContext.Response.Headers[ProductContactUsTabNoIndex.HeaderName] = "index, follow, noarchive";
            nextContext.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await application.Build()(context);

        Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
            Is.EqualTo("noarchive, noindex, follow"));
    }

    [Test]
    public async Task OnStartingRemovesConflictingNoneDirectiveAndPreservesNeutralTokens()
    {
        var context = CreateContext(HttpMethods.Get, "/en/filterSearch");
        var feature = new StartingResponseFeature();
        context.Features.Set<IHttpResponseFeature>(feature);

        Assert.That(ProductContactUsTabNoIndex.TryApply(context), Is.True);
        context.Response.Headers[ProductContactUsTabNoIndex.HeaderName] = "none, noarchive";

        await feature.FireOnStartingAsync();

        Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
            Is.EqualTo("noarchive, noindex, follow"));
        Assert.That(feature.HasStarted, Is.True);
    }

    [Test]
    public void LocaleLessAllRoutesAreEligible()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProductContactUsTabNoIndex.TryGetHeaderValue(CreateContext(HttpMethods.Get, "/producttag/all").Request, out _), Is.True);
            Assert.That(ProductContactUsTabNoIndex.TryGetHeaderValue(CreateContext(HttpMethods.Get, "/manufacturer/all/").Request, out _), Is.True);
        });
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
        new ProductContactUsTabNoIndexNopStartup().Configure(application);
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            return Task.CompletedTask;
        });
        return application.Build();
    }

    private sealed class StartingResponseFeature : IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _callbacks = new();

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            _callbacks.Push((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            while (_callbacks.TryPop(out var callback))
                await callback.Callback(callback.State);

            HasStarted = true;
        }
    }
}
