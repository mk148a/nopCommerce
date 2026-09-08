using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

public class HoodProductShippingDimensionExclusionBuilder : NopEntityBuilder<HoodProductShippingDimensionExclusion>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(HoodProductShippingDimensionExclusion.Reason)).AsString(1000).Nullable();
    }
}
