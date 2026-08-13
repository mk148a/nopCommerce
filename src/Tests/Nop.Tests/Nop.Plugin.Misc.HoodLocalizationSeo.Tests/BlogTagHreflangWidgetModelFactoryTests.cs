using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Plugin.Misc.HoodLocalizationSeo.Factories;
using Nop.Web.Framework.Factories;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Models.Cms;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class BlogTagHreflangWidgetModelFactoryTests
{
    private static readonly Type GoogleLanguageComponent = BuildGoogleLanguageComponentType();

    [Test]
    public async Task RemovesGenericGoogleLanguageWidgetFromBlogTagHeadZone()
    {
        var otherComponent = typeof(BlogTagHreflangWidgetModelFactoryTests);
        var inner = new StubWidgetModelFactory(GoogleLanguageComponent, otherComponent);
        var context = CreateContext("Blog", "BlogByTag");
        var factory = new BlogTagHreflangWidgetModelFactory(inner,
            new HttpContextAccessor { HttpContext = context });

        var result = await factory.PrepareRenderWidgetModelAsync(PublicWidgetZones.HeadHtmlTag);

        result.Select(model => model.WidgetViewComponent).Should().Equal(otherComponent);
    }

    [Test]
    public async Task KeepsGenericGoogleLanguageWidgetOnOtherRoutes()
    {
        var otherComponent = typeof(BlogTagHreflangWidgetModelFactoryTests);
        var inner = new StubWidgetModelFactory(GoogleLanguageComponent, otherComponent);
        var context = CreateContext("Blog", "List");
        var factory = new BlogTagHreflangWidgetModelFactory(inner,
            new HttpContextAccessor { HttpContext = context });

        var result = await factory.PrepareRenderWidgetModelAsync(PublicWidgetZones.HeadHtmlTag);

        result.Select(model => model.WidgetViewComponent)
            .Should().Equal(GoogleLanguageComponent, otherComponent);
    }

    [Test]
    public async Task KeepsGenericGoogleLanguageWidgetOutsideHeadZone()
    {
        var inner = new StubWidgetModelFactory(GoogleLanguageComponent);
        var context = CreateContext("Blog", "BlogByTag");
        var factory = new BlogTagHreflangWidgetModelFactory(inner,
            new HttpContextAccessor { HttpContext = context });

        var result = await factory.PrepareRenderWidgetModelAsync("content_before");

        result.Select(model => model.WidgetViewComponent).Should().Equal(GoogleLanguageComponent);
    }

    private static DefaultHttpContext CreateContext(string controller, string action)
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary
        {
            ["controller"] = controller,
            ["action"] = action
        };
        return context;
    }

    private static Type BuildGoogleLanguageComponentType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Tests");
        return module.DefineType(
                "Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency.Components.GoogleMultiLanguageAndCurrencyWidget",
                TypeAttributes.Public | TypeAttributes.Class)
            .CreateType();
    }

    private sealed class StubWidgetModelFactory : IWidgetModelFactory
    {
        private readonly Type[] _componentTypes;

        public StubWidgetModelFactory(params Type[] componentTypes)
        {
            _componentTypes = componentTypes;
        }

        public Task<List<RenderWidgetModel>> PrepareRenderWidgetModelAsync(
            string widgetZone,
            object additionalData = null,
            bool useCache = true)
        {
            return Task.FromResult(_componentTypes.Select(type => new RenderWidgetModel
            {
                WidgetViewComponent = type
            }).ToList());
        }
    }
}
