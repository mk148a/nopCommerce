using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

[NopMigration("2026/05/21 15:10:00:0000000", "Shipping.FixedByWeightByTotal Hood/Navlungo shipping driver attributes", MigrationProcessType.Update)]
public class AddHoodProductShippingDriverAttribute : AutoReversingMigration
{
    public override void Up()
    {
        if (!Schema.Table(nameof(HoodProductShippingDriverAttribute)).Exists())
            Create.TableFor<HoodProductShippingDriverAttribute>();
    }
}
