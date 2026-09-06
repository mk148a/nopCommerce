using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.Misc.HoodLocalizationSeo.Consumers;
using Nop.Plugin.Misc.HoodLocalizationSeo.Services;
using NUnit.Framework;

namespace Nop.Plugin.Misc.HoodLocalizationSeo.Tests;

[TestFixture]
public sealed class SitemapProductChangeConsumerTests
{
    [Test]
    public async Task InsertUpdateDelete_each_invalidate_generated_sitemaps_independently()
    {
        var invalidator = new Mock<ISitemapArtifactInvalidator>();
        var consumer = new SitemapProductChangeConsumer(invalidator.Object);
        var product = new Product { Id = 17 };

        await consumer.HandleEventAsync(new EntityUpdatedEvent<Product>(product));
        invalidator.Verify(x => x.InvalidateGeneratedXmlFiles(), Times.Once);
        invalidator.Invocations.Clear();

        await consumer.HandleEventAsync(new EntityDeletedEvent<Product>(product));
        invalidator.Verify(x => x.InvalidateGeneratedXmlFiles(), Times.Once);
        invalidator.Invocations.Clear();

        await consumer.HandleEventAsync(new EntityInsertedEvent<Product>(product));
        invalidator.Verify(x => x.InvalidateGeneratedXmlFiles(), Times.Once);
    }
}
