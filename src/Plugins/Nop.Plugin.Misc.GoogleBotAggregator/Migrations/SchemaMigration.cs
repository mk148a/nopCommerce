using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.GoogleBotAggregator.Domain;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Migrations;

[NopMigration("2024/02/23 12:00:00", "Nop.Plugin.Misc.GoogleBotAggregator schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        Create.TableFor<GoogleBotCustomer>();
    }
}