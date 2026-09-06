using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Services.Events;
using Nop.Web.Infrastructure.Cache;

namespace Nop.Plugin.Widgets.HoodAnswerSeo.Infrastructure;

/// <summary>
/// Keeps the visible product-review summary and its schema extension aligned
/// when a review is created or moderated. nopCommerce clears this presentation
/// cache on deletion, but a newly approved review also changes the aggregate.
/// </summary>
public sealed class ReviewOverviewCacheEventConsumer :
    IConsumer<EntityInsertedEvent<ProductReview>>,
    IConsumer<EntityUpdatedEvent<ProductReview>>
{
    private readonly IStaticCacheManager _staticCacheManager;

    public ReviewOverviewCacheEventConsumer(IStaticCacheManager staticCacheManager)
    {
        _staticCacheManager = staticCacheManager;
    }

    public Task HandleEventAsync(EntityInsertedEvent<ProductReview> eventMessage)
        => ClearProductReviewCacheAsync(eventMessage.Entity.ProductId);

    public Task HandleEventAsync(EntityUpdatedEvent<ProductReview> eventMessage)
        => ClearProductReviewCacheAsync(eventMessage.Entity.ProductId);

    private Task ClearProductReviewCacheAsync(int productId)
    {
        if (productId <= 0)
            return Task.CompletedTask;

        return _staticCacheManager.RemoveByPrefixAsync(
            string.Format(NopModelCacheDefaults.ProductReviewsPrefixCacheKeyById, productId));
    }
}
