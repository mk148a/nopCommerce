using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;

public sealed class SitemapProductChangeConsumer :
    IConsumer<EntityUpdatedEvent<Product>>,
    IConsumer<EntityDeletedEvent<Product>>,
    IConsumer<EntityInsertedEvent<Product>>
{
    private readonly ISitemapArtifactInvalidator _invalidator;

    public SitemapProductChangeConsumer(ISitemapArtifactInvalidator invalidator)
    {
        _invalidator = invalidator;
    }

    public Task HandleEventAsync(EntityUpdatedEvent<Product> eventMessage)
    {
        _invalidator.InvalidateGeneratedXmlFiles();
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(EntityDeletedEvent<Product> eventMessage)
    {
        _invalidator.InvalidateGeneratedXmlFiles();
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(EntityInsertedEvent<Product> eventMessage)
    {
        _invalidator.InvalidateGeneratedXmlFiles();
        return Task.CompletedTask;
    }
}
