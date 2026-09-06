using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Migrations;

[NopMigration("2026/08/12 12:00:00:0000000", "Misc.StripeBnplCore fee reconciliation",
    MigrationProcessType.Update)]
public class FeeReconciliationMigration : Migration
{
    public override void Up()
    {
        var tableName = NameCompatibilityManager.GetTableName(typeof(StripeBnplPaymentRecord));
        if (!Schema.Table(tableName).Exists())
            return;

        // Existing rows are intentionally queued again. Re-reading Stripe is safer
        // than assuming a legacy zero represented an actual zero processing fee.
        if (!Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeDataStatus)).Exists())
            Alter.Table(tableName)
                .AddColumn(nameof(StripeBnplPaymentRecord.FeeDataStatus)).AsString(32).NotNullable()
                .WithDefaultValue(StripeBnplFeeDataStatus.Pending);
        if (!Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeDataError)).Exists())
            Alter.Table(tableName)
                .AddColumn(nameof(StripeBnplPaymentRecord.FeeDataError)).AsString(1000).Nullable();
        if (!Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeReconciliationAttemptCount)).Exists())
            Alter.Table(tableName)
                .AddColumn(nameof(StripeBnplPaymentRecord.FeeReconciliationAttemptCount)).AsInt32().NotNullable()
                .WithDefaultValue(0);
        if (!Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeLastAttemptOnUtc)).Exists())
            Alter.Table(tableName)
                .AddColumn(nameof(StripeBnplPaymentRecord.FeeLastAttemptOnUtc)).AsDateTime2().Nullable();
        if (!Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeCompletedOnUtc)).Exists())
            Alter.Table(tableName)
                .AddColumn(nameof(StripeBnplPaymentRecord.FeeCompletedOnUtc)).AsDateTime2().Nullable();

        // A missing Stripe BalanceTransaction is unknown, never zero. Nullable
        // amounts make accidental reporting of the legacy placeholder impossible.
        if (Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.FeeMinor)).Exists())
            Alter.Column(nameof(StripeBnplPaymentRecord.FeeMinor)).OnTable(tableName).AsInt64().Nullable();
        if (Schema.Table(tableName).Column(nameof(StripeBnplPaymentRecord.NetMinor)).Exists())
            Alter.Column(nameof(StripeBnplPaymentRecord.NetMinor)).OnTable(tableName).AsInt64().Nullable();
    }

    public override void Down()
    {
        var tableName = NameCompatibilityManager.GetTableName(typeof(StripeBnplPaymentRecord));
        if (!Schema.Table(tableName).Exists())
            return;

        foreach (var column in new[]
                 {
                     nameof(StripeBnplPaymentRecord.FeeCompletedOnUtc),
                     nameof(StripeBnplPaymentRecord.FeeLastAttemptOnUtc),
                     nameof(StripeBnplPaymentRecord.FeeReconciliationAttemptCount),
                     nameof(StripeBnplPaymentRecord.FeeDataError),
                     nameof(StripeBnplPaymentRecord.FeeDataStatus)
                 })
        {
            if (Schema.Table(tableName).Column(column).Exists())
                Delete.Column(column).FromTable(tableName);
        }
    }
}
