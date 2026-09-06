using FluentAssertions;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Localization;

[TestFixture]
public class LocalizedSeoContractTests
{
    [Test]
    public void Blog_storefront_uses_localized_fields_slugs_and_default_language_posts()
    {
        var factory = ReadSource("src", "Presentation", "Nop.Web", "Factories", "BlogModelFactory.cs");
        var controller = ReadSource("src", "Presentation", "Nop.Web", "Controllers", "BlogController.cs");
        var entity = ReadSource("src", "Libraries", "Nop.Core", "Domain", "Blogs", "BlogPost.cs");

        factory.Should().Contain("GetLocalizedAsync(blogPost, post => post.Title, languageId");
        factory.Should().Contain("GetSeNameAsync(blogPost, languageId, ensureTwoPublishedLanguages: false)");
        factory.Should().Contain("GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId");
        controller.Should().Contain("GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId)");
        controller.Should().Contain("GetLocalizedAsync(store, x => x.Name, language.Id)");
        controller.Should().Contain("AddLanguageSeoCodeToUrl(Request.PathBase, true, language)");
        entity.Should().Contain("ILocalizedEntity, ISlugSupported");
    }

    [Test]
    public void Html_sitemap_uses_localized_blog_titles_slugs_and_default_language_posts()
    {
        var factory = ReadSource("src", "Presentation", "Nop.Web", "Factories", "SitemapModelFactory.cs");

        factory.Should().Contain("GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId)");
        factory.Should().Contain("GetLocalizedAsync(post, x => x.Title, language.Id)");
        factory.Should().Contain("GetSeNameAsync(post, language.Id, ensureTwoPublishedLanguages: false)");
    }

    [Test]
    public void Localized_slug_redirects_are_permanent()
    {
        var transformer = ReadSource("src", "Presentation", "Nop.Web.Framework", "Mvc", "Routing", "SlugRouteTransformer.cs");

        transformer.Should().Contain("$\"/{language.UniqueSeoCode}/{slugLocalized}\", true");
        transformer.Should().Contain("$\"/{language.UniqueSeoCode}/{activeCatalogSlug}/{activeSlug}\", true");
        transformer.Should().Contain("redirectPath = $\"/{language.UniqueSeoCode}/{slug}\"");
        transformer.Should().NotContain("$\"/{language.UniqueSeoCode}/{slugLocalized}\", false");
    }

    [Test]
    public void Hreflang_widget_includes_the_current_language()
    {
        var widget = ReadSource("src", "Plugins", "Nop.Plugin.Misc.GoogleMultiLanguageAndCurrency", "Components", "GoogleMultiLanguageAndCurrencyWidget.cs");

        widget.Should().NotContain("activeLanguagee.Id == currentLanguageId");
        widget.Should().NotContain("language.Id == currentLanguageId");
    }

    [Test]
    public void Homepage_blog_and_contact_views_emit_canonical_urls()
    {
        var views = new[]
        {
            ReadSource("src", "Presentation", "Nop.Web", "Views", "Home", "Index.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Views", "Blog", "List.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Views", "Blog", "BlogPost.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Views", "Common", "ContactUs.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Views", "Common", "Sitemap.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Home", "Index.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Blog", "List.cshtml"),
            ReadSource("src", "Presentation", "Nop.Web", "Themes", "Element", "Views", "Blog", "BlogPost.cshtml")
        };

        views.Should().AllSatisfy(view => view.Should().Contain("AddCanonicalUrlParts"));
    }

    [Test]
    public void Contact_form_uses_topic_seo_and_legacy_topic_url_is_permanent()
    {
        var view = ReadSource("src", "Presentation", "Nop.Web", "Views", "Common", "ContactUs.cshtml");
        var controller = ReadSource("src", "Presentation", "Nop.Web", "Controllers", "TopicController.cs");
        var sitemap = ReadSource("src", "Presentation", "Nop.Web", "Factories", "SitemapModelFactory.cs");

        view.Should().Contain("topic => topic.MetaDescription");
        view.Should().Contain("topic => topic.MetaTitle");
        controller.Should().Contain("RedirectToRoutePermanent(\"ContactUs\")");
        sitemap.Should().Contain("!t.SystemName.Equals(\"ContactUs\"",
            "the XML sitemap must exclude the dedicated ContactUs topic");
        sitemap.Should().Contain("!topic.SystemName.Equals(\"ContactUs\"",
            "the HTML sitemap must exclude the dedicated ContactUs topic");
    }

    [Test]
    public void Thin_product_tag_archives_are_noindex()
    {
        var tag = ReadSource("src", "Presentation", "Nop.Web", "Views", "Catalog", "ProductsByTag.cshtml");
        var tags = ReadSource("src", "Presentation", "Nop.Web", "Views", "Catalog", "ProductTagsAll.cshtml");

        tag.Should().Contain("noindex,follow");
        tags.Should().Contain("noindex,follow");
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Presentation", "Nop.Web")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test must run from a repository build output");
        return File.ReadAllText(Path.Combine(directory!.FullName, Path.Combine(parts)));
    }
}
