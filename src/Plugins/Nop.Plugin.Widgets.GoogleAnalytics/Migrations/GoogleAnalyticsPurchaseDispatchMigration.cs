using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Migrations;

[NopMigration("2026-08-22 09:00:00", "Widgets.GoogleAnalytics paid purchase dispatch idempotency", MigrationProcessType.Update)]
public class GoogleAnalyticsPurchaseDispatchMigration : Migration
{
    public override void Up()
    {
        if (Schema.Table(nameof(GoogleAnalyticsPurchaseDispatch)).Exists())
            return;

        Create.TableFor<GoogleAnalyticsPurchaseDispatch>();

        Create.Index("IX_GoogleAnalyticsPurchaseDispatch_OrderId")
            .OnTable(nameof(GoogleAnalyticsPurchaseDispatch))
            .OnColumn(nameof(GoogleAnalyticsPurchaseDispatch.OrderId)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        if (Schema.Table(nameof(GoogleAnalyticsPurchaseDispatch)).Exists())
            Delete.Table(nameof(GoogleAnalyticsPurchaseDispatch));
    }
}
