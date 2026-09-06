using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Mapping;

public class StripeBnplWebhookEventBuilder : NopEntityBuilder<StripeBnplWebhookEvent>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(StripeBnplWebhookEvent.EventId)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.EventType)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.ObjectId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.OrderId)).AsInt32().Nullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.ProcessingStatus)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.ProcessingToken)).AsString(64).Nullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.LeaseExpiresOnUtc)).AsNopDateTime2().Nullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.Error)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.EventCreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.CreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplWebhookEvent.ProcessedOnUtc)).AsNopDateTime2().NotNullable();
    }
}
