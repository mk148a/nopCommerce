using System.Security.Cryptography;
using System.Text;
using Nop.Core.Domain.Blogs;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Models.Blogs;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Services;

public interface IBlogLocalizationService
{
    Task ApplyAsync(BlogPostModel model, BlogPost blogPost, int languageId);
    Task<string> GetFieldAsync(BlogPost blogPost, string key, int languageId, string fallback = null);
    Task<string> GetSlugAsync(BlogPost blogPost, int languageId);
    Task<string> GetTagLabelAsync(string rawTag, int languageId);
}

/// <summary>
/// Reads BlogPost localized properties without requiring BlogPost to implement
/// ILocalizedEntity. That keeps the plugin binary compatible with an unmodified
/// nopCommerce 4.80 core assembly.
/// </summary>
public sealed class BlogLocalizationService : IBlogLocalizationService
{
    public const string LocaleKeyGroup = "BlogPost";

    private readonly ILocalizedEntityService _localizedEntityService;
    private readonly ILocalizationService _localizationService;
    private readonly IUrlRecordService _urlRecordService;

    public BlogLocalizationService(ILocalizedEntityService localizedEntityService,
        ILocalizationService localizationService,
        IUrlRecordService urlRecordService)
    {
        _localizedEntityService = localizedEntityService;
        _localizationService = localizationService;
        _urlRecordService = urlRecordService;
    }

    public async Task ApplyAsync(BlogPostModel model, BlogPost blogPost, int languageId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(blogPost);

        model.MetaTitle = await GetFieldAsync(blogPost, nameof(BlogPost.MetaTitle), languageId, blogPost.MetaTitle);
        model.MetaDescription = await GetFieldAsync(blogPost, nameof(BlogPost.MetaDescription), languageId, blogPost.MetaDescription);
        model.MetaKeywords = await GetFieldAsync(blogPost, nameof(BlogPost.MetaKeywords), languageId, blogPost.MetaKeywords);
        model.Title = await GetFieldAsync(blogPost, nameof(BlogPost.Title), languageId, blogPost.Title);
        model.Body = await GetFieldAsync(blogPost, nameof(BlogPost.Body), languageId, blogPost.Body);
        model.BodyOverview = await GetFieldAsync(blogPost, nameof(BlogPost.BodyOverview), languageId, blogPost.BodyOverview);
        model.SeName = await GetSlugAsync(blogPost, languageId);
    }

    public async Task<string> GetFieldAsync(BlogPost blogPost, string key, int languageId, string fallback = null)
    {
        ArgumentNullException.ThrowIfNull(blogPost);

        var value = languageId > 0
            ? await _localizedEntityService.GetLocalizedValueAsync(languageId, blogPost.Id, LocaleKeyGroup, key)
            : null;

        return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    public async Task<string> GetSlugAsync(BlogPost blogPost, int languageId)
    {
        ArgumentNullException.ThrowIfNull(blogPost);

        var slug = await _urlRecordService.GetSeNameAsync(blogPost.Id, nameof(BlogPost), languageId,
            returnDefaultValue: true, ensureTwoPublishedLanguages: false);

        return slug ?? string.Empty;
    }

    public Task<string> GetTagLabelAsync(string rawTag, int languageId)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
            return Task.FromResult(string.Empty);

        return _localizationService.GetResourceAsync($"Hood.BlogTag.{BuildStableKey(rawTag)}",
            languageId, logIfNotFound: false, defaultValue: rawTag);
    }

    private static string BuildStableKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16].ToLowerInvariant();
    }
}
