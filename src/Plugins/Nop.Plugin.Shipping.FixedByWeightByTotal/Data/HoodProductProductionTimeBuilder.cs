using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

public class HoodProductProductionTimeBuilder : NopEntityBuilder<HoodProductProductionTime>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(HoodProductProductionTime.ProductionTimeText)).AsString(400).Nullable()
            .WithColumn(nameof(HoodProductProductionTime.Message)).AsString(1000).Nullable()
            .WithColumn(nameof(HoodProductProductionTime.Source)).AsString(200).Nullable();
    }
}
