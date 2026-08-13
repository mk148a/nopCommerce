using System.Collections;
using System.Reflection;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Events;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Services.Events;
using Nop.Services.Seo;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Blogs;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

/// <summary>
/// Localizes SevenSpikes RichBlog projection models without a compile-time
/// reference to the commercial assembly. Only a narrowly allow-listed set of
/// public properties is changed; only the current post URL is regenerated,
/// while vendor filter keys remain untouched.
/// </summary>
public sealed class RichBlogModelEventConsumer : IConsumer<ModelPreparedEvent<BaseNopModel>>
{
    private static readonly string[] EntityIdProperties =
    {
        "BlogPostId", "Id"
    };

    private static readonly string[] CurrentTitleProperties =
    {
        "BlogPostTitle", "Title",
        "ImageAlt", "ImageTitle", "PictureAlt", "PictureTitle", "PinterestDescription"
    };

    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogRouteLanguageResolver _blogRouteLanguageResolver;
    private readonly IBlogService _blogService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IUrlRecordService _urlRecordService;

    public RichBlogModelEventConsumer(IBlogLocalizationService blogLocalizationService,
        IBlogRouteLanguageResolver blogRouteLanguageResolver,
        IBlogService blogService,
        INopUrlHelper nopUrlHelper,
        IUrlRecordService urlRecordService)
    {
        _blogLocalizationService = blogLocalizationService;
        _blogRouteLanguageResolver = blogRouteLanguageResolver;
        _blogService = blogService;
        _nopUrlHelper = nopUrlHelper;
        _urlRecordService = urlRecordService;
    }

    public async Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        var model = eventMessage?.Model;
        if (model is null || !model.GetType().FullName!.StartsWith("SevenSpikes.Nop.Plugins.RichBlog.",
                StringComparison.Ordinal))
            return;

        var language = await _blogRouteLanguageResolver.ResolveAsync();
        await LocalizeObjectGraphAsync(model, language.Id,
            new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
    }

    private async Task LocalizeObjectGraphAsync(object model, int languageId,
        ISet<object> visited, int depth)
    {
        if (model is null || depth > 3 || !visited.Add(model))
            return;

        var type = model.GetType();
        var blogPost = await ResolveBlogPostAsync(model);
        if (blogPost is not null)
        {
            var title = await _blogLocalizationService.GetFieldAsync(blogPost, nameof(BlogPost.Title), languageId,
                blogPost.Title);
            foreach (var propertyName in CurrentTitleProperties)
                SetStringProperty(model, propertyName, title);

            // RichBlog implementations use several names for the generated
            // slug. These are safe to change because they identify this same
            // BlogPost, unlike tag/filter URLs.
            var slug = await _blogLocalizationService.GetSlugAsync(blogPost, languageId);
            SetStringProperty(model, "SeName", slug);
            SetStringProperty(model, "BlogPostSeName", slug);
            if (!string.IsNullOrWhiteSpace(slug))
            {
                var url = await _nopUrlHelper.RouteGenericUrlAsync<BlogPost>(new { SeName = slug });
                SetStringProperty(model, "BlogPostUrl", url);
            }
        }

        await LocalizeRelatedReferenceAsync(model, "PreviousBlogPostId", "PreviousBlogPostTitle",
            "PreviousBlogPostSeName", languageId);
        await LocalizeRelatedReferenceAsync(model, "NextBlogPostId", "NextBlogPostTitle",
            "NextBlogPostSeName", languageId);
        await LocalizeRelatedReferenceAsync(model, "RelatedBlogPostId", "RelatedBlogPostTitle",
            "RelatedBlogPostSeName", languageId);

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            object value;
            try
            {
                value = property.GetValue(model);
            }
            catch
            {
                continue;
            }

            if (value is string text)
            {
                if (property.CanWrite && property.PropertyType == typeof(string) &&
                    property.Name.Equals("Tags", StringComparison.OrdinalIgnoreCase))
                    property.SetValue(model, await LocalizeCommaSeparatedTagsAsync(text, languageId));
                continue;
            }

            if (value is null)
                continue;

            if (IsTagCollection(property.Name) && value is IList tagList)
            {
                for (var index = 0; index < tagList.Count; index++)
                {
                    if (tagList[index] is string rawTag && !tagList.IsReadOnly && !tagList.IsFixedSize)
                    {
                        tagList[index] = await _blogLocalizationService.GetTagLabelAsync(rawTag, languageId);
                        continue;
                    }

                    var tagModel = tagList[index];
                    var nameProperty = tagModel?.GetType().GetProperty("Name",
                        BindingFlags.Instance | BindingFlags.Public);
                    if (nameProperty?.CanRead == true && nameProperty.CanWrite &&
                        nameProperty.PropertyType == typeof(string) && nameProperty.GetValue(tagModel) is string name)
                        nameProperty.SetValue(tagModel,
                            await _blogLocalizationService.GetTagLabelAsync(name, languageId));
                }
            }

            if (value is IEnumerable collection)
            {
                foreach (var item in collection.Cast<object>().Where(item => item is not null).Take(100))
                    await LocalizeObjectGraphAsync(item, languageId, visited, depth + 1);
            }
            else if (value.GetType().Namespace?.StartsWith("SevenSpikes.Nop.Plugins.RichBlog.",
                         StringComparison.Ordinal) == true)
            {
                await LocalizeObjectGraphAsync(value, languageId, visited, depth + 1);
            }
        }
    }

    private async Task<BlogPost> ResolveBlogPostAsync(object model)
    {
        foreach (var propertyName in EntityIdProperties)
        {
            var property = model.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property?.CanRead != true)
                continue;

            var value = property.GetValue(model);
            if (value is int id && id > 0)
            {
                var post = await _blogService.GetBlogPostByIdAsync(id);
                if (post is not null)
                    return post;
            }
        }

        var slugProperty = model.GetType().GetProperty("SeName", BindingFlags.Instance | BindingFlags.Public)
            ?? model.GetType().GetProperty("BlogPostSeName", BindingFlags.Instance | BindingFlags.Public);
        if (slugProperty?.GetValue(model) is not string slug || string.IsNullOrWhiteSpace(slug))
            return null;

        // Use UrlRecord's entity id when RichBlog did not expose it directly.
        var record = await _urlRecordService.GetBySlugAsync(slug);
        return record?.EntityName.Equals(nameof(BlogPost), StringComparison.OrdinalIgnoreCase) == true
            ? await _blogService.GetBlogPostByIdAsync(record.EntityId)
            : null;
    }

    private async Task LocalizeRelatedReferenceAsync(object model, string idPropertyName,
        string titlePropertyName, string slugPropertyName, int languageId)
    {
        var idProperty = model.GetType().GetProperty(idPropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (idProperty?.GetValue(model) is not int id || id <= 0)
            return;

        var post = await _blogService.GetBlogPostByIdAsync(id);
        if (post is null)
            return;

        var title = await _blogLocalizationService.GetFieldAsync(post, nameof(BlogPost.Title), languageId, post.Title);
        SetStringProperty(model, titlePropertyName, title);
        SetStringProperty(model, slugPropertyName, await _blogLocalizationService.GetSlugAsync(post, languageId));
    }

    private static void SetStringProperty(object model, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var property = model.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.CanWrite == true && property.PropertyType == typeof(string))
            property.SetValue(model, value);
    }

    private async Task<string> LocalizeCommaSeparatedTagsAsync(string value, int languageId)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < tags.Length; index++)
            tags[index] = await _blogLocalizationService.GetTagLabelAsync(tags[index], languageId);
        return string.Join(", ", tags);
    }

    private static bool IsTagCollection(string propertyName) =>
        propertyName.Equals("Tags", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("BlogPostTags", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("TagModels", StringComparison.OrdinalIgnoreCase);

}
