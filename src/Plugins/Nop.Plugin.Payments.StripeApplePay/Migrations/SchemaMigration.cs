using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Payments.StripeApplePay.Migrations
{
    [NopMigration("", "Nop.Plugin.Payments.StripeApplePay schema", MigrationProcessType.Installation)]
    public class SchemaMigration : AutoReversingMigration
    {
        private readonly IMigrationManager _migrationManager;

        public SchemaMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
        }
    }
}
