using FluentMigrator;
using Nop.Core.Domain.Stores;
using Nop.Data.Mapping;

namespace Nop.Data.Migrations.UpgradeTo450
{
    [NopMigration("2021/12/07 16:00:00:0000000", "Store soft deleting", MigrationProcessType.Update)]
    public class StoreMigration : ForwardOnlyMigration
    {
        public override void Up()
        {
            if (!Schema.Table(NameCompatibilityManager.GetTableName(typeof(Store))).Column(nameof(Store.Deleted)).Exists())
            {
                //add new column
                Alter.Table(NameCompatibilityManager.GetTableName(typeof(Store)))
                    .AddColumn(nameof(Store.Deleted)).AsBoolean().WithDefaultValue(false);

                Alter.Table(NameCompatibilityManager.GetTableName(typeof(Store)))
                    .AlterColumn(nameof(Store.Deleted)).AsBoolean();
            }
        }
    }
}