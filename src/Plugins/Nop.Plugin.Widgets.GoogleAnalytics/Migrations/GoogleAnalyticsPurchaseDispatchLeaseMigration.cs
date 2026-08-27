using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Migrations;

/// <summary>
/// Extends the prior OrderId-unique dispatch table with a recoverable browser delivery lease.
/// </summary>
[NopMigration("2026-08-27 10:45:00", "Widgets.GoogleAnalytics purchase dispatch lease", MigrationProcessType.Update)]
public class GoogleAnalyticsPurchaseDispatchLeaseMigration : Migration
{
    public override void Up()
    {
        var tableName = nameof(GoogleAnalyticsPurchaseDispatch);
        if (!Schema.Table(tableName).Exists())
        {
            Create.TableFor<GoogleAnalyticsPurchaseDispatch>();
            Create.Index("IX_GoogleAnalyticsPurchaseDispatch_OrderId")
                .OnTable(tableName)
                .OnColumn(nameof(GoogleAnalyticsPurchaseDispatch.OrderId)).Ascending()
                .WithOptions().Unique();
            return;
        }

        // Existing rows predate leases and therefore represent already-completed
        // browser dispatches. Preserve their duplicate-suppression status.
        if (!Schema.Table(tableName).Column(nameof(GoogleAnalyticsPurchaseDispatch.Status)).Exists())
            Alter.Table(tableName).AddColumn(nameof(GoogleAnalyticsPurchaseDispatch.Status))
                .AsString(16).NotNullable().WithDefaultValue("confirmed");
        if (!Schema.Table(tableName).Column(nameof(GoogleAnalyticsPurchaseDispatch.LeaseToken)).Exists())
            Alter.Table(tableName).AddColumn(nameof(GoogleAnalyticsPurchaseDispatch.LeaseToken)).AsString(64).Nullable();
        if (!Schema.Table(tableName).Column(nameof(GoogleAnalyticsPurchaseDispatch.LeaseExpiresOnUtc)).Exists())
            Alter.Table(tableName).AddColumn(nameof(GoogleAnalyticsPurchaseDispatch.LeaseExpiresOnUtc)).AsDateTime2().Nullable();
        if (!Schema.Table(tableName).Column(nameof(GoogleAnalyticsPurchaseDispatch.ConfirmedOnUtc)).Exists())
            Alter.Table(tableName).AddColumn(nameof(GoogleAnalyticsPurchaseDispatch.ConfirmedOnUtc)).AsDateTime2().Nullable();

        // The current production table already has this index. Retain the
        // guard for installations where legacy confirmed rows migrated before
        // the index was introduced: do not create a second index, but
        // restore the OrderId uniqueness contract when it is absent.
        if (!Schema.Table(tableName).Index("IX_GoogleAnalyticsPurchaseDispatch_OrderId").Exists())
            Create.Index("IX_GoogleAnalyticsPurchaseDispatch_OrderId")
                .OnTable(tableName)
                .OnColumn(nameof(GoogleAnalyticsPurchaseDispatch.OrderId)).Ascending()
                .WithOptions().Unique();
    }

    public override void Down()
    {
        // Do not drop the durable dispatch table or its unique OrderId contract on downgrade.
    }
}
