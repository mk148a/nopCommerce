using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Mapping;

public class StripeBnplPaymentRecordBuilder : NopEntityBuilder<StripeBnplPaymentRecord>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(StripeBnplPaymentRecord.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.ProviderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.SessionId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.PaymentIntentId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.ChargeId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.BalanceTransactionId)).AsString(255).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.PaymentMethodType)).AsString(64).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.Status)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.AmountMinor)).AsInt64().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeMinor)).AsInt64().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.NetMinor)).AsInt64().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.RefundedAmountMinor)).AsInt64().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.PendingRefundAmountMinor)).AsInt64().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.RefundClaimedOnUtc)).AsNopDateTime2().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.Currency)).AsString(3).NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.SettlementCurrency)).AsString(3).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.ExchangeRate)).AsDecimal(18, 8).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeDetailsJson)).AsString(int.MaxValue).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeDataStatus)).AsString(32).NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeDataError)).AsString(1000).Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeReconciliationAttemptCount)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeLastAttemptOnUtc)).AsNopDateTime2().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.FeeCompletedOnUtc)).AsNopDateTime2().Nullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.IsSandbox)).AsBoolean().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.CreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplPaymentRecord.UpdatedOnUtc)).AsNopDateTime2().NotNullable();
    }
}
