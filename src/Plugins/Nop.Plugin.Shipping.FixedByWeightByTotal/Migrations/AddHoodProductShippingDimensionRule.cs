using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

[NopMigration("2026/05/20 12:20:00:0000000", "Shipping.FixedByWeightByTotal Hood/Navlungo dimension rules", MigrationProcessType.Update)]
public class AddHoodProductShippingDimensionRule : AutoReversingMigration
{
    public override void Up()
    {
        if (!Schema.Table(nameof(HoodProductShippingDimensionRule)).Exists())
            Create.TableFor<HoodProductShippingDimensionRule>();
    }
}
