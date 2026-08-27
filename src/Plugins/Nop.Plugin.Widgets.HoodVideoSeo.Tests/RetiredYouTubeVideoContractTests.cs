using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Nop.Core;
using Nop.Plugin.Widgets.HoodVideoSeo.Infrastructure;
using Nop.Plugin.Widgets.HoodVideoSeo.Services;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.Media;
using NUnit.Framework;

namespace Nop.Plugin.Widgets.HoodVideoSeo.Tests;

[TestFixture]
public sealed class RetiredYouTubeVideoContractTests
{
    [TestCase("tPxPcvSjLJk")]
    [TestCase("B7H4igrP0Wo")]
    [TestCase("FxSceVSLx4U")]
    public void RetiredVideoIsSuppressedBeforeAvailabilityLookup(string youtubeId)
    {
        var availabilityService = new Mock<IYouTubeVideoAvailabilityService>(MockBehavior.Strict);

        Assert.That(RetiredYouTubeVideos.ShouldRender(youtubeId, availabilityService.Object), Is.False);
        availabilityService.VerifyNoOtherCalls();
    }

    [TestCase("tPxPcvSjLJk")]
    [TestCase("B7H4igrP0Wo")]
    [TestCase("FxSceVSLx4U")]
    public async Task ProductResultFilterRemovesRetiredMappedVideoWithoutAvailabilityLookup(string youtubeId)
    {
        var workContext = new Mock<IWorkContext>(MockBehavior.Strict);
        var availabilityService = new Mock<IYouTubeVideoAvailabilityService>(MockBehavior.Strict);
        var filter = new YouTubeEmbedResultFilter(workContext.Object, availabilityService.Object);
        var product = new ProductDetailsModel
        {
            VideoModels = new List<VideoModel>
            {
                new() { VideoUrl = $"https://www.youtube.com/embed/{youtubeId}" }
            }
        };
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var result = new ViewResult
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = product
            }
        };
        var executing = new ResultExecutingContext(actionContext, new List<IFilterMetadata>(), result, new object());

        await filter.OnResultExecutionAsync(executing, () => Task.FromResult(
            new ResultExecutedContext(actionContext, new List<IFilterMetadata>(), result, new object())));

        Assert.That(product.VideoModels, Is.Empty);
        workContext.VerifyNoOtherCalls();
        availabilityService.VerifyNoOtherCalls();
    }

    [Test]
    public void ProductVideosPartialUsesTheSharedLocalSuppressionGuard()
    {
        var view = File.ReadAllText(FindPluginFile("Views", "Product", "_ProductDetailsVideos.cshtml"));

        Assert.That(view, Does.Contain("RetiredYouTubeVideos.ShouldRender(item.YouTubeId, VideoAvailabilityService)"));
    }

    private static string FindPluginFile(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var pluginRoot = Path.Combine(directory.FullName, "Nop.Plugin.Widgets.HoodVideoSeo");
            var candidate = Path.Combine(new[] { pluginRoot }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
                return candidate;

            pluginRoot = Path.Combine(directory.FullName, "src", "Plugins", "Nop.Plugin.Widgets.HoodVideoSeo");
            candidate = Path.Combine(new[] { pluginRoot }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
                return candidate;
        }

        Assert.Fail("Could not locate the HoodVideoSeo plugin source from the test output directory.");
        return string.Empty;
    }
}
