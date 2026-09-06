using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Migrations;

[NopMigration("2026/08/12 12:30:00:0000000", "Misc.StripeBnplCore immutable eligibility and processing leases",
    MigrationProcessType.Update)]
public class CheckoutAtomicityMigration : Migration
{
    public override void Up()
    {
        var sessionTable = NameCompatibilityManager.GetTableName(typeof(StripeBnplCheckoutSession));
        if (Schema.Table(sessionTable).Exists())
        {
            // Nullable only for an upgrade from an unpublished preview schema.
            // Such legacy attempts deliberately fail closed at finalization;
            // every newly reserved attempt writes both fields.
            if (!Schema.Table(sessionTable).Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotJson)).Exists())
                Alter.Table(sessionTable)
                    .AddColumn(nameof(StripeBnplCheckoutSession.EligibilitySnapshotJson)).AsString(int.MaxValue).Nullable();
            if (!Schema.Table(sessionTable).Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotHash)).Exists())
                Alter.Table(sessionTable)
                    .AddColumn(nameof(StripeBnplCheckoutSession.EligibilitySnapshotHash)).AsString(64).Nullable();
        }

        var webhookTable = NameCompatibilityManager.GetTableName(typeof(StripeBnplWebhookEvent));
        if (Schema.Table(webhookTable).Exists())
        {
            if (!Schema.Table(webhookTable).Column(nameof(StripeBnplWebhookEvent.ProcessingToken)).Exists())
                Alter.Table(webhookTable)
                    .AddColumn(nameof(StripeBnplWebhookEvent.ProcessingToken)).AsString(64).Nullable();
            if (!Schema.Table(webhookTable).Column(nameof(StripeBnplWebhookEvent.LeaseExpiresOnUtc)).Exists())
                Alter.Table(webhookTable)
                    .AddColumn(nameof(StripeBnplWebhookEvent.LeaseExpiresOnUtc)).AsDateTime2().Nullable();
        }
    }

    public override void Down()
    {
        var sessionTable = NameCompatibilityManager.GetTableName(typeof(StripeBnplCheckoutSession));
        if (Schema.Table(sessionTable).Exists())
        {
            if (Schema.Table(sessionTable).Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotHash)).Exists())
                Delete.Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotHash)).FromTable(sessionTable);
            if (Schema.Table(sessionTable).Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotJson)).Exists())
                Delete.Column(nameof(StripeBnplCheckoutSession.EligibilitySnapshotJson)).FromTable(sessionTable);
        }

        var webhookTable = NameCompatibilityManager.GetTableName(typeof(StripeBnplWebhookEvent));
        if (Schema.Table(webhookTable).Exists())
        {
            if (Schema.Table(webhookTable).Column(nameof(StripeBnplWebhookEvent.LeaseExpiresOnUtc)).Exists())
                Delete.Column(nameof(StripeBnplWebhookEvent.LeaseExpiresOnUtc)).FromTable(webhookTable);
            if (Schema.Table(webhookTable).Column(nameof(StripeBnplWebhookEvent.ProcessingToken)).Exists())
                Delete.Column(nameof(StripeBnplWebhookEvent.ProcessingToken)).FromTable(webhookTable);
        }
    }
}
