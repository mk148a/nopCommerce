using Moq;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Localization;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Seo;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.Routing;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests
{
    [TestFixture]
    public sealed class RichBlogModelEventConsumerTests
    {
        [Test]
        public async Task LocalizesVendorImageModelSlugTitleAndBlogPostUrl()
        {
            var post = new BlogPost { Id = 12, Title = "Viking and medieval boots" };
            var german = new Language { Id = 7, UniqueSeoCode = "de", Published = true };
            const string localizedTitle = "Handgefertigte Wikinger- und Mittelalterstiefel";
            const string localizedSlug = "handgefertigte-wikinger-und-mittelalterstiefel";
            const string localizedUrl = "/de/handgefertigte-wikinger-und-mittelalterstiefel";
            var model = new SevenSpikes.Nop.Plugins.RichBlog.Models.FakeRichBlogImageModel
            {
                BlogPostId = post.Id,
                BlogPostTitle = post.Title,
                Title = post.Title,
                SeName = "viking-and-medieval-boots",
                BlogPostSeName = "viking-and-medieval-boots",
                BlogPostUrl = "/en/viking-and-medieval-boots"
            };

            var localization = new Mock<IBlogLocalizationService>();
            localization.Setup(service => service.GetFieldAsync(post, nameof(BlogPost.Title), german.Id,
                    post.Title))
                .ReturnsAsync(localizedTitle);
            localization.Setup(service => service.GetSlugAsync(post, german.Id))
                .ReturnsAsync(localizedSlug);
            var resolver = new Mock<IBlogRouteLanguageResolver>();
            resolver.Setup(service => service.ResolveAsync()).ReturnsAsync(german);
            var blogService = new Mock<IBlogService>();
            blogService.Setup(service => service.GetBlogPostByIdAsync(post.Id)).ReturnsAsync(post);
            var urlHelper = new Mock<INopUrlHelper>();
            urlHelper.Setup(helper => helper.RouteGenericUrlAsync<BlogPost>(
                    It.Is<object>(values => ReadSeName(values) == localizedSlug), null, null, null))
                .ReturnsAsync(localizedUrl);

            var consumer = new RichBlogModelEventConsumer(localization.Object, resolver.Object,
                blogService.Object, urlHelper.Object, Mock.Of<IUrlRecordService>());

            await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

            Assert.Multiple(() =>
            {
                Assert.That(model.BlogPostTitle, Is.EqualTo(localizedTitle));
                Assert.That(model.Title, Is.EqualTo(localizedTitle));
                Assert.That(model.SeName, Is.EqualTo(localizedSlug));
                Assert.That(model.BlogPostSeName, Is.EqualTo(localizedSlug));
                Assert.That(model.BlogPostUrl, Is.EqualTo(localizedUrl));
            });
            urlHelper.VerifyAll();
        }

        [Test]
        public async Task IgnoresNonVendorModelWithoutResolvingServices()
        {
            var model = new NonVendorProjection
            {
                BlogPostId = 12,
                BlogPostUrl = "/en/viking-and-medieval-boots"
            };
            var consumer = new RichBlogModelEventConsumer(
                new Mock<IBlogLocalizationService>(MockBehavior.Strict).Object,
                new Mock<IBlogRouteLanguageResolver>(MockBehavior.Strict).Object,
                new Mock<IBlogService>(MockBehavior.Strict).Object,
                new Mock<INopUrlHelper>(MockBehavior.Strict).Object,
                new Mock<IUrlRecordService>(MockBehavior.Strict).Object);

            await consumer.HandleEventAsync(new ModelPreparedEvent<BaseNopModel>(model));

            Assert.That(model.BlogPostUrl, Is.EqualTo("/en/viking-and-medieval-boots"));
        }

        private static string ReadSeName(object values) =>
            values?.GetType().GetProperty("SeName")?.GetValue(values)?.ToString();

        private sealed record NonVendorProjection : BaseNopModel
        {
            public int BlogPostId { get; init; }
            public string BlogPostUrl { get; set; }
        }
    }
}

namespace SevenSpikes.Nop.Plugins.RichBlog.Models
{
    public sealed record FakeRichBlogImageModel : BaseNopModel
    {
        public int BlogPostId { get; init; }
        public string BlogPostTitle { get; set; }
        public string Title { get; set; }
        public string SeName { get; set; }
        public string BlogPostSeName { get; set; }
        public string BlogPostUrl { get; set; }
    }
}
