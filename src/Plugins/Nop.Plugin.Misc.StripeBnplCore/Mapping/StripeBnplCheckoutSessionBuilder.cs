using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Mapping;

public class StripeBnplCheckoutSessionBuilder : NopEntityBuilder<StripeBnplCheckoutSession>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(StripeBnplCheckoutSession.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.ProviderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.SessionId)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.CheckoutUrl)).AsString(2048).Nullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.PaymentIntentId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.AmountMinor)).AsInt64().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.Currency)).AsString(3).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.Status)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.AttemptNumber)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.IdempotencyKey)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.EligibilitySnapshotJson)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.EligibilitySnapshotHash)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.ExpiresOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.IsSandbox)).AsBoolean().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.CreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplCheckoutSession.UpdatedOnUtc)).AsNopDateTime2().NotNullable();
    }
}
