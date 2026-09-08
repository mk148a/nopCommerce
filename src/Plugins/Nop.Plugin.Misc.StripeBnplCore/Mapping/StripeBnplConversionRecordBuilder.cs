using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Mapping;

public class StripeBnplConversionRecordBuilder : NopEntityBuilder<StripeBnplConversionRecord>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(StripeBnplConversionRecord.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.ProviderId)).AsInt32().NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.TransactionId)).AsString(255).NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.PaymentType)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.Value)).AsDecimal(18, 4).NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.Currency)).AsString(3).NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.Status)).AsString(64).NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.CreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(StripeBnplConversionRecord.ConsumedOnUtc)).AsNopDateTime2().Nullable();
    }
}
