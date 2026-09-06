using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Core.Http;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Services.Localization;
using Nop.Services.Catalog;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Mvc.Routing;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class RoutingEventConsumerTests
{
    [Test]
    public async Task DeletedProductRoutesToPluginPageNotFoundWithoutRedirect()
    {
        var language = new Language { Id = 2, Published = true, UniqueSeoCode = "tr" };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync())
            .ReturnsAsync(new Store { Id = 1, DefaultLanguageId = language.Id });
        var languages = new Mock<ILanguageService>();
        languages.Setup(service => service.GetAllLanguagesAsync(false, 1))
            .ReturnsAsync(new List<Language> { language });
        var products = new Mock<IProductService>();
        products.Setup(service => service.GetProductByIdAsync(48))
            .ReturnsAsync(new Product { Id = 48, Deleted = true });
        var httpContext = new DefaultHttpContext();
        var routeValues = new RouteValueDictionary
        {
            [NopRoutingDefaults.RouteValue.Language] = "tr"
        };
        var routingEvent = new GenericRoutingEvent(httpContext, routeValues,
            new UrlRecord { EntityId = 48, EntityName = nameof(Product), LanguageId = language.Id,
                Slug = "deleted-product", IsActive = true });
        var consumer = new RoutingEventConsumer(languages.Object, storeContext.Object,
            products.Object, Mock.Of<ITopicService>(), Mock.Of<IUrlRecordService>());

        await consumer.HandleEventAsync(routingEvent);

        var stopProperty = routingEvent.GetType().GetProperty("StopProcessing")
                           ?? routingEvent.GetType().GetProperty("Handled");
        Assert.Multiple(() =>
        {
            Assert.That(stopProperty?.GetValue(routingEvent), Is.EqualTo(true));
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.Controller], Is.EqualTo("Common"));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.Action], Is.EqualTo("PageNotFound"));
            Assert.That(routeValues.ContainsKey(NopRoutingDefaults.RouteValue.PermanentRedirect), Is.False);
        });
    }

    [Test]
    public async Task RetiredGeneratedSlugPermanentlyRedirectsToCurrentLocalizedSlug()
    {
        var language = new Language { Id = 2, Published = true, UniqueSeoCode = "tr" };
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync())
            .ReturnsAsync(new Store { Id = 1, DefaultLanguageId = language.Id });
        var languages = new Mock<ILanguageService>();
        languages.Setup(service => service.GetAllLanguagesAsync(false, 1))
            .ReturnsAsync(new List<Language> { language });
        var urlRecords = new Mock<IUrlRecordService>();
        urlRecords.Setup(service => service.GetActiveSlugAsync(48, "Category", language.Id))
            .ReturnsAsync("turk-okculugu-yuzugu");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/shop";
        httpContext.Request.QueryString = new QueryString("?page=2");
        var routeValues = new RouteValueDictionary
        {
            [NopRoutingDefaults.RouteValue.Language] = "tr"
        };
        var routingEvent = new GenericRoutingEvent(httpContext, routeValues,
            new UrlRecord
            {
                Id = 123,
                EntityId = 48,
                EntityName = "Category",
                LanguageId = language.Id,
                Slug = "tuerk-okculugu-yuezuegu",
                IsActive = false
            });
        var consumer = new RoutingEventConsumer(languages.Object, storeContext.Object,
            Mock.Of<IProductService>(), Mock.Of<ITopicService>(), urlRecords.Object);

        await consumer.HandleEventAsync(routingEvent);

        Assert.Multiple(() =>
        {
            var stopProperty = routingEvent.GetType().GetProperty("StopProcessing")
                               ?? routingEvent.GetType().GetProperty("Handled");
            Assert.That(stopProperty?.GetValue(routingEvent), Is.EqualTo(true));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.Controller], Is.EqualTo("Common"));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.Action], Is.EqualTo("InternalRedirect"));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.Url],
                Is.EqualTo("/shop/tr/turk-okculugu-yuzugu?page=2"));
            Assert.That(routeValues[NopRoutingDefaults.RouteValue.PermanentRedirect], Is.EqualTo(true));
            Assert.That(httpContext.Items[NopHttpDefaults.GenericRouteInternalRedirect], Is.EqualTo(true));
        });
    }
}
