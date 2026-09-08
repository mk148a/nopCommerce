using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Shipping.FixedByWeightByTotal.Domain;

namespace Nop.Plugin.Shipping.FixedByWeightByTotal.Data;

[NopMigration("2026/05/24 15:00:00:0000000", "Shipping.FixedByWeightByTotal Hood production time", MigrationProcessType.Update)]
public class AddHoodProductProductionTime : AutoReversingMigration
{
    public override void Up()
    {
        if (!Schema.Table(nameof(HoodProductProductionTime)).Exists())
            Create.TableFor<HoodProductProductionTime>();
    }
}
