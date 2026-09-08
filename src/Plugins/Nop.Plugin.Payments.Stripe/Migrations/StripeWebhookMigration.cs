using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.Stripe.Domains;

namespace Nop.Plugin.Payments.Stripe.Migrations;

[NopMigration("2026-08-04 18:00:00", "Payments.Stripe webhook event idempotency", MigrationProcessType.Update)]
public class StripeWebhookMigration : Migration
{
    public override void Up()
    {
        if (Schema.Table(nameof(StripeWebhookEvent)).Exists())
            return;

        Create.TableFor<StripeWebhookEvent>();

        Create.Index("IX_StripeWebhookEvent_EventId")
            .OnTable(nameof(StripeWebhookEvent))
            .OnColumn(nameof(StripeWebhookEvent.EventId)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        if (Schema.Table(nameof(StripeWebhookEvent)).Exists())
            Delete.Table(nameof(StripeWebhookEvent));
    }
}
