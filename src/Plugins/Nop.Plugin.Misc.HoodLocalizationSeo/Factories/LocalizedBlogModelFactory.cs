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
    private readonly IBlogRouteLanguageResolver _routeLanguageResolver;

    public LocalizedBlogModelFactory(IBlogModelFactory inner,
        BlogSettings blogSettings,
        IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IBlogRouteLanguageResolver routeLanguageResolver)
    {
        _inner = inner;
        _blogSettings = blogSettings;
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _staticCacheManager = staticCacheManager;
        _storeContext = storeContext;
        _routeLanguageResolver = routeLanguageResolver;
    }

    public async Task PrepareBlogPostModelAsync(BlogPostModel model, BlogPost blogPost, bool prepareComments)
    {
        await _inner.PrepareBlogPostModelAsync(model, blogPost, prepareComments);
        var language = await _routeLanguageResolver.ResolveAsync();
        await _blogLocalizationService.ApplyAsync(model, blogPost, language.Id);
        for (var index = 0; index < model.Tags.Count; index++)
            model.Tags[index] = await _blogLocalizationService.GetTagLabelAsync(model.Tags[index], language.Id);
    }

    public async Task<BlogPostListModel> PrepareBlogPostListModelAsync(BlogPagingFilteringModel command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PageSize <= 0)
            command.PageSize = _blogSettings.PostsPageSize;
        if (command.PageNumber <= 0)
            command.PageNumber = 1;

        var language = await _routeLanguageResolver.ResolveAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var requestedTag = command.Tag;
        var sourceTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(requestedTag))
        {
            var allTags = await _blogService.GetAllBlogPostTagsAsync(store.Id, store.DefaultLanguageId);
            foreach (var candidate in allTags)
            {
                var label = await _blogLocalizationService.GetTagLabelAsync(candidate.Name, language.Id);
                if (label.Equals(requestedTag, StringComparison.OrdinalIgnoreCase) ||
                    candidate.Name.Equals(requestedTag, StringComparison.OrdinalIgnoreCase))
                    sourceTags.Add(candidate.Name);
            }
            if (sourceTags.Count == 0)
                sourceTags.Add(requestedTag);
        }
        var dateFrom = command.GetFromMonth();
        var dateTo = command.GetToMonth();

        // Hood has one canonical/default-language BlogPost row per article.
        // LocalizedProperty and UrlRecord hold the per-language projections.
        IPagedList<BlogPost> blogPosts;
        if (sourceTags.Count == 0)
        {
            blogPosts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId, dateFrom, dateTo,
                command.PageNumber - 1, command.PageSize);
        }
        else
        {
            // Distinct English source tags can legitimately translate to the
            // same customer-facing label. Query their union so a localized tag
            // link never drops posts merely because one alias was encountered
            // first.
            var allPosts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId);
            var matchingPosts = new List<BlogPost>();
            foreach (var post in allPosts)
            {
                var tags = await _blogService.ParseTagsAsync(post);
                if (tags.Any(sourceTags.Contains))
                    matchingPosts.Add(post);
            }
            blogPosts = new PagedList<BlogPost>(matchingPosts, command.PageNumber - 1, command.PageSize);
        }

        var model = new BlogPostListModel
        {
            PagingFilteringContext = { Tag = requestedTag, Month = command.Month },
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
        var language = await _routeLanguageResolver.ResolveAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var tags = await _blogService.GetAllBlogPostTagsAsync(store.Id, store.DefaultLanguageId);
        var labelBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
            labelBySource[tag.Name] = await _blogLocalizationService.GetTagLabelAsync(tag.Name, language.Id);

        // Collapse aliases that share a reviewed localized label. Count each
        // post once even when its source metadata contains multiple aliases.
        var countByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var labelSpelling = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var posts = await _blogService.GetAllBlogPostsAsync(store.Id, store.DefaultLanguageId);
        foreach (var post in posts)
        {
            var labelsOnPost = (await _blogService.ParseTagsAsync(post))
                .Where(labelBySource.ContainsKey)
                .Select(source => labelBySource[source])
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var label in labelsOnPost)
            {
                labelSpelling.TryAdd(label, label);
                countByLabel[label] = countByLabel.GetValueOrDefault(label) + 1;
            }
        }
        var localizedTags = countByLabel
            .Select(pair => new BlogPostTagModel
            {
                Name = labelSpelling[pair.Key],
                BlogPostCount = pair.Value
            })
            .OrderByDescending(tag => tag.BlogPostCount)
            .Take(_blogSettings.NumberOfTags)
            .OrderBy(tag => tag.Name, StringComparer.CurrentCulture);

        foreach (var tag in localizedTags)
            model.Tags.Add(new BlogPostTagModel
            {
                Name = tag.Name,
                BlogPostCount = tag.BlogPostCount
            });
        return model;
    }

    public async Task<List<BlogPostYearModel>> PrepareBlogPostYearModelAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var currentLanguage = await _routeLanguageResolver.ResolveAsync();
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

}
