using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

[NopMigration("2026/05/20 13:35:00:0000000", "Shipping.FixedByWeightByTotal Hood/Navlungo dimension exclusions", MigrationProcessType.Update)]
public class AddHoodProductShippingDimensionExclusion : AutoReversingMigration
{
    public override void Up()
    {
        if (!Schema.Table(nameof(HoodProductShippingDimensionExclusion)).Exists())
            Create.TableFor<HoodProductShippingDimensionExclusion>();
    }
}
