using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

public class HoodProductShippingDriverAttributeBuilder : NopEntityBuilder<HoodProductShippingDriverAttribute>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(HoodProductShippingDriverAttribute.ProductAttributeName)).AsString(400).Nullable()
            .WithColumn(nameof(HoodProductShippingDriverAttribute.RuleType)).AsString(50).NotNullable();
    }
}
