using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Blogs;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Infrastructure;

/// <summary>
/// Corrects the core BlogCommentAdd success redirect, which otherwise uses the
/// BlogPost row's source language slug instead of the request language slug.
/// </summary>
public sealed class LocalizedBlogRedirectFilter : IAsyncResultFilter
{
    private readonly IBlogLocalizationService _blogLocalizationService;
    private readonly IBlogService _blogService;
    private readonly IWorkContext _workContext;

    public LocalizedBlogRedirectFilter(IBlogLocalizationService blogLocalizationService,
        IBlogService blogService,
        IWorkContext workContext)
    {
        _blogLocalizationService = blogLocalizationService;
        _blogService = blogService;
        _workContext = workContext;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Controller is Nop.Web.Controllers.BlogController &&
            context.ActionDescriptor.RouteValues.TryGetValue("action", out var action) &&
            action.Equals("BlogCommentAdd", StringComparison.OrdinalIgnoreCase) &&
            context.Result is LocalRedirectResult redirect &&
            TryGetBlogPostId(context, out var blogPostId))
        {
            var blogPost = await _blogService.GetBlogPostByIdAsync(blogPostId);
            var language = await _workContext.GetWorkingLanguageAsync();
            if (blogPost is not null)
            {
                var localizedSlug = await _blogLocalizationService.GetSlugAsync(blogPost, language.Id);
                if (!string.IsNullOrWhiteSpace(localizedSlug))
                    redirect.Url = $"/{language.UniqueSeoCode}/{localizedSlug}";
            }
        }

        await next();
    }

    private static bool TryGetBlogPostId(FilterContext context, out int blogPostId)
    {
        blogPostId = 0;
        return context.RouteData.Values.TryGetValue("blogPostId", out var value) &&
               int.TryParse(value?.ToString(), out blogPostId) && blogPostId > 0;
    }
}
