using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Blogs;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;
using Nop.Web.Factories;
using Nop.Web.Infrastructure.Cache;
using Nop.Web.Models.Blogs;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Factories;

/// <summary>
/// Composes the core factory instead of inheriting from it, so future changes
/// to the concrete BlogModelFactory constructor do not become a binary
/// dependency of this plugin.
/// </summary>
public sealed class LocalizedBlogModelFactory : IBlogModelFactory
{
    private readonly IBlogModelFactory _inner;
    private readonly BlogSettings _blogSettings;
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly IStaticCacheManager _staticCacheManager;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public LocalizedBlogModelFactory(IBlogModelFactory inner,
        BlogSettings blogSettings,
        IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _inner = inner;
        _blogSettings = blogSettings;
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _staticCacheManager = staticCacheManager;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task PrepareBlogPostModelAsync(BlogPostModel model, BlogPost blogPost, bool prepareComments)
    {
        await _inner.PrepareBlogPostModelAsync(model, blogPost, prepareComments);
        var language = await _workContext.GetWorkingLanguageAsync();
        await _blogLocalizationService.ApplyAsync(model, blogPost, language.Id);
        if (!IsEnglish(language.LanguageCulture))
            model.Tags.Clear();
    }

    public async Task<BlogPostListModel> PrepareBlogPostListModelAsync(BlogPagingFilteringModel command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PageSize <= 0)
            command.PageSize = _blogSettings.PostsPageSize;
        if (command.PageNumber <= 0)
            command.PageNumber = 1;

        var language = await _workContext.GetWorkingLanguageAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var dateFrom = command.GetFromMonth();
        var dateTo = command.GetToMonth();

        // Hood has one canonical/default-language BlogPost row per article.
        // LocalizedProperty and UrlRecord hold the per-language projections.
        var blogPosts = string.IsNullOrEmpty(command.Tag)
            ? await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId, dateFrom, dateTo,
                command.PageNumber - 1, command.PageSize)
            : await _blogService.GetAllBlogPostsByTagAsync(store.Id, store.DefaultLanguageId, command.Tag,
                command.PageNumber - 1, command.PageSize);

        var model = new BlogPostListModel
        {
            PagingFilteringContext = { Tag = command.Tag, Month = command.Month },
            WorkingLanguageId = language.Id,
            BlogPosts = await blogPosts.SelectAwait(async blogPost =>
            {
                var postModel = new BlogPostModel();
                await PrepareBlogPostModelAsync(postModel, blogPost, false);
                return postModel;
            }).ToListAsync()
        };

        model.PagingFilteringContext.LoadPagedList(blogPosts);
        return model;
    }

    public async Task<BlogPostTagListModel> PrepareBlogPostTagListModelAsync()
    {
        var model = new BlogPostTagListModel();
        var language = await _workContext.GetWorkingLanguageAsync();
        if (!IsEnglish(language.LanguageCulture))
            return model;

        var store = await _storeContext.GetCurrentStoreAsync();
        var tags = (await _blogService.GetAllBlogPostTagsAsync(store.Id, store.DefaultLanguageId))
            .OrderByDescending(tag => tag.BlogPostCount)
            .Take(_blogSettings.NumberOfTags);

        // Keep Name as the raw filter key. Views/RichBlog models translate only
        // the visible label so tag URLs continue to select the canonical data.
        model.Tags.AddRange(tags.OrderBy(tag => tag.Name).Select(tag => new BlogPostTagModel
        {
            Name = tag.Name,
            BlogPostCount = tag.BlogPostCount
        }));
        return model;
    }

    public async Task<List<BlogPostYearModel>> PrepareBlogPostYearModelAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var currentLanguage = await _workContext.GetWorkingLanguageAsync();
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(
            NopModelCacheDefaults.BlogMonthsModelKey, currentLanguage, store);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var model = new List<BlogPostYearModel>();
            var blogPosts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId);
            if (!blogPosts.Any())
                return model;

            var months = new SortedDictionary<DateTime, int>();
            var firstPost = blogPosts[blogPosts.Count - 1];
            var first = firstPost.StartDateUtc ?? firstPost.CreatedOnUtc;
            while (DateTime.SpecifyKind(first, DateTimeKind.Utc) <= DateTime.UtcNow.AddMonths(1))
            {
                var monthStart = new DateTime(first.Year, first.Month, 1);
                var list = await _blogService.GetPostsByDateAsync(blogPosts, monthStart,
                    monthStart.AddMonths(1).AddSeconds(-1));
                if (list.Any())
                    months[monthStart] = list.Count;
                first = first.AddMonths(1);
            }

            var currentYear = 0;
            foreach (var (date, count) in months)
            {
                if (currentYear == 0)
                    currentYear = date.Year;
                if (date.Year > currentYear || !model.Any())
                    model.Insert(0, new BlogPostYearModel { Year = date.Year });

                model.First().Months.Insert(0, new BlogPostMonthModel
                {
                    Month = date.Month,
                    BlogPostCount = count
                });
                currentYear = date.Year;
            }

            return model;
        });
    }

    public Task<BlogCommentModel> PrepareBlogPostCommentModelAsync(BlogComment blogComment)
    {
        return _inner.PrepareBlogPostCommentModelAsync(blogComment);
    }

    private static bool IsEnglish(string culture) =>
        culture?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
}
