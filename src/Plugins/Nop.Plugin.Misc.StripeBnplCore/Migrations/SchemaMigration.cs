using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Migrations;

[NopMigration("2026/08/11 20:00:00:0000000", "Misc.StripeBnplCore schema", MigrationProcessType.Installation)]
public class SchemaMigration : Migration
{
    public override void Up()
    {
        Create.TableFor<BnplProductEligibility>();
        Create.TableFor<StripeBnplCheckoutSession>();
        Create.TableFor<StripeBnplWebhookEvent>();
        Create.TableFor<StripeBnplPaymentRecord>();
        Create.TableFor<StripeBnplConversionRecord>();

        Create.Index("IX_BnplProductEligibility_Product_Provider")
            .OnTable(nameof(BnplProductEligibility))
            .OnColumn(nameof(BnplProductEligibility.ProductId)).Ascending()
            .OnColumn(nameof(BnplProductEligibility.ProviderId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_StripeBnplCheckoutSession_SessionId")
            .OnTable(nameof(StripeBnplCheckoutSession))
            .OnColumn(nameof(StripeBnplCheckoutSession.SessionId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_StripeBnplCheckoutSession_Order_Provider")
            .OnTable(nameof(StripeBnplCheckoutSession))
            .OnColumn(nameof(StripeBnplCheckoutSession.OrderId)).Ascending()
            .OnColumn(nameof(StripeBnplCheckoutSession.ProviderId)).Ascending();

        Create.Index("IX_StripeBnplCheckoutSession_Order_Provider_Attempt")
            .OnTable(nameof(StripeBnplCheckoutSession))
            .OnColumn(nameof(StripeBnplCheckoutSession.OrderId)).Ascending()
            .OnColumn(nameof(StripeBnplCheckoutSession.ProviderId)).Ascending()
            .OnColumn(nameof(StripeBnplCheckoutSession.AttemptNumber)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_StripeBnplWebhookEvent_EventId")
            .OnTable(nameof(StripeBnplWebhookEvent))
            .OnColumn(nameof(StripeBnplWebhookEvent.EventId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_StripeBnplPaymentRecord_OrderId")
            .OnTable(nameof(StripeBnplPaymentRecord))
            .OnColumn(nameof(StripeBnplPaymentRecord.OrderId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_StripeBnplConversionRecord_OrderId")
            .OnTable(nameof(StripeBnplConversionRecord))
            .OnColumn(nameof(StripeBnplConversionRecord.OrderId)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table(nameof(StripeBnplConversionRecord));
        Delete.Table(nameof(StripeBnplPaymentRecord));
        Delete.Table(nameof(StripeBnplWebhookEvent));
        Delete.Table(nameof(StripeBnplCheckoutSession));
        Delete.Table(nameof(BnplProductEligibility));
    }
}
