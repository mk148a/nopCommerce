using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class ProductContactUsTabNoIndexTests
{
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("HEAD", "/en/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("HEAD", "/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("GET", "/EN/producttab/productcontactustab/190/", "ProductTab", "ProductContactUsTab", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("GET", "/en/filterSearch", "Catalog7Spikes", "AjaxFiltersSearch", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/fr/filterSearch/", "Catalog7Spikes", "AjaxFiltersSearch", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/en/recentlyviewedproducts", "Product", "RecentlyViewedProducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/fr/compareproducts", "Product", "CompareProducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/filterSearch", "Common", "GenericUrl", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/recentlyviewedproducts/", "Common", "GenericUrl", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/compareproducts", "Common", "GenericUrl", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/recentlyviewedproducts", "Product", "RecentlyViewedProducts", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/no/profile/1423974", "Profile", "Index", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("HEAD", "/profile/0/", "Profile", "Index", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/no/profile/1423974/page/2/", "Profile", "Index", ProductContactUsTabNoIndex.UtilityEndpointHeaderValue)]
    [TestCase("GET", "/au/customer/checkgiftcardbalance", "Customer", "CheckGiftCardBalance", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    [TestCase("HEAD", "/customer/checkgiftcardbalance", "Common", "GenericUrl", ProductContactUsTabNoIndex.ProductContactUsTabHeaderValue)]
    public async Task RoutedEligibleEndpointAddsNoIndexHeaderAndPreservesResponse(
        string method,
        string path,
        string controller,
        string action,
        string expectedHeaderValue)
    {
        var context = CreateContext(method, path, controller, action);
        context.Request.QueryString = new QueryString("?utm_source=google&tab=contact");

        await BuildPipeline()(context);
        await GetResponseFeature(context).FireOnStartingAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status202Accepted));
            Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
                Is.EqualTo(expectedHeaderValue));
            Assert.That(context.Request.QueryString.ToString(), Is.EqualTo("?utm_source=google&tab=contact"));
        });
    }

    [TestCase("POST", "/en/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/not-a-number", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/en/ProductTab/ProductContactUsTab/190/extra", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/en/ProductTab/ProductReviewsTab/190", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/en/wooden-ottoman-hunting-arrows", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("GET", "/Admin/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/en/filterSearch/extra", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("GET", "/en/filterSearchResults", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("GET", "/en/recentlyviewedproducts/190", "Product", "RecentlyViewedProducts")]
    [TestCase("GET", "/en/compareproducts/190", "Product", "CompareProducts")]
    [TestCase("GET", "/en/filterSearch", "Common", "GenericUrl")]
    [TestCase("GET", "/en/recentlyviewedproducts", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("GET", "/en/compareproducts", "Product", "RecentlyViewedProducts")]
    [TestCase("GET", "/pt-BR/ProductTab/ProductContactUsTab/190", "ProductTab", "ProductContactUsTab")]
    [TestCase("GET", "/pt-BR/filterSearch", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("GET", "/pt-BR/recentlyviewedproducts", "Product", "RecentlyViewedProducts")]
    [TestCase("GET", "/pt-BR/compareproducts", "Product", "CompareProducts")]
    [TestCase("GET", "/en-US/filterSearch", "Catalog7Spikes", "AjaxFiltersSearch")]
    [TestCase("POST", "/no/profile/1423974", "Profile", "Index")]
    [TestCase("GET", "/no/profile/not-a-number", "Profile", "Index")]
    [TestCase("GET", "/no/profile/1423974/page/not-a-number", "Profile", "Index")]
    [TestCase("GET", "/no/profile/-1", "Profile", "Index")]
    [TestCase("GET", "/no/profile/1423974/page/2/extra", "Profile", "Index")]
    [TestCase("GET", "/no/profile/1423974", "Customer", "Index")]
    [TestCase("GET", "/au/customer/checkgiftcardbalance/extra", "Customer", "CheckGiftCardBalance")]
    [TestCase("POST", "/au/customer/checkgiftcardbalance", "Customer", "CheckGiftCardBalance")]
    public async Task NonMatchingPathsMethodsOrEndpointsDoNotReceiveNoIndexHeader(
        string method,
        string path,
        string controller,
        string action)
    {
        var context = CreateContext(method, path, controller, action);

        await BuildPipeline()(context);
        await GetResponseFeature(context).FireOnStartingAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status202Accepted));
            Assert.That(context.Response.Headers.ContainsKey(ProductContactUsTabNoIndex.HeaderName), Is.False);
        });
    }

    [Test]
    public void StartedResponseIsNeverModified()
    {
        var context = CreateContext(HttpMethods.Get, "/en/ProductTab/ProductContactUsTab/190",
            "ProductTab", "ProductContactUsTab");
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
    public async Task OnStartingMergeWinsOverDownstreamOverwrite()
    {
        var context = CreateContext(HttpMethods.Get, "/en/filterSearch", "Catalog7Spikes", "AjaxFiltersSearch");
        var application = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        new ProductContactUsTabNoIndexNopStartup().Configure(application);
        application.Run(nextContext =>
        {
            nextContext.Response.Headers[ProductContactUsTabNoIndex.HeaderName] = "index, follow, noarchive";
            nextContext.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await application.Build()(context);
        await GetResponseFeature(context).FireOnStartingAsync();

        Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
            Is.EqualTo("noarchive, noindex, follow"));
    }

    [Test]
    public async Task OnStartingRemovesConflictingNoneDirectiveAndPreservesNeutralTokens()
    {
        var context = CreateContext(HttpMethods.Get, "/en/filterSearch", "Catalog7Spikes", "AjaxFiltersSearch");

        Assert.That(ProductContactUsTabNoIndex.TryApply(context), Is.True);
        context.Response.Headers[ProductContactUsTabNoIndex.HeaderName] = "none, noarchive";
        await GetResponseFeature(context).FireOnStartingAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
                Is.EqualTo("noarchive, noindex, follow"));
            Assert.That(GetResponseFeature(context).HasStarted, Is.True);
        });
    }

    [Test]
    public void LocaleLessAllRoutesAreEligible()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProductContactUsTabNoIndex.TryGetHeaderValue(
                CreateContext(HttpMethods.Get, "/producttag/all", "Catalog", "ProductTagsAll").Request, out _), Is.True);
            Assert.That(ProductContactUsTabNoIndex.TryGetHeaderValue(
                CreateContext(HttpMethods.Get, "/manufacturer/all/", "Catalog", "ManufacturerAll").Request, out _), Is.True);
        });
    }

    [Test]
    public async Task LocalizedNewProductsQueryMergesItsDirectiveOnStarting()
    {
        var context = CreateContext(HttpMethods.Get, "/en/newproducts", "Catalog", "NewProducts");
        context.Request.QueryString = new QueryString("?pagenumber=2");
        context.Response.Headers[ProductContactUsTabNoIndex.HeaderName] = "index, noarchive";

        await BuildPipeline()(context);
        await GetResponseFeature(context).FireOnStartingAsync();

        Assert.That(context.Response.Headers[ProductContactUsTabNoIndex.HeaderName].ToString(),
            Is.EqualTo("noarchive, noindex, follow"));
    }

    private static DefaultHttpContext CreateContext(string method, string path, string controller = null, string action = null)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new DeferredResponseFeature());
        context.Request.Method = method;
        context.Request.Path = path;
        if (!string.IsNullOrWhiteSpace(controller) && !string.IsNullOrWhiteSpace(action))
        {
            context.Request.RouteValues = new RouteValueDictionary
            {
                ["controller"] = controller,
                ["action"] = action
            };
        }

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

    private static DeferredResponseFeature GetResponseFeature(DefaultHttpContext context) =>
        (DeferredResponseFeature)context.Features.Get<IHttpResponseFeature>();

    private sealed class DeferredResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            for (var index = _onStarting.Count - 1; index >= 0; index--)
                await _onStarting[index].Callback(_onStarting[index].State);

            HasStarted = true;
        }
    }
}
