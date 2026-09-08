using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.Stripe.Domains;

namespace Nop.Plugin.Payments.Stripe.Mapping.Builders;

public class StripeWebhookEventBuilder : NopEntityBuilder<StripeWebhookEvent>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(StripeWebhookEvent.EventId)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeWebhookEvent.EventType)).AsString(255).Nullable()
            .WithColumn(nameof(StripeWebhookEvent.PaymentIntentId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeWebhookEvent.OrderId)).AsInt32().Nullable()
            .WithColumn(nameof(StripeWebhookEvent.ProcessedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeWebhookEvent.ProcessingStatus)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeWebhookEvent.Error)).AsString(int.MaxValue).Nullable();
    }
}
