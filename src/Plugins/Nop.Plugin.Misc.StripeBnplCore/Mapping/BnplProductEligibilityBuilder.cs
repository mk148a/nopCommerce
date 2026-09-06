using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.StripeBnplCore.Domain;

namespace Nop.Plugin.Misc.StripeBnplCore.Mapping;

public class BnplProductEligibilityBuilder : NopEntityBuilder<BnplProductEligibility>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(BnplProductEligibility.ProductId)).AsInt32().NotNullable()
            .WithColumn(nameof(BnplProductEligibility.ProviderId)).AsInt32().NotNullable()
            .WithColumn(nameof(BnplProductEligibility.EligibilityStateId)).AsInt32().NotNullable()
            .WithColumn(nameof(BnplProductEligibility.Reason)).AsString(1000).Nullable()
            .WithColumn(nameof(BnplProductEligibility.FulfillmentDays)).AsInt32().Nullable()
            .WithColumn(nameof(BnplProductEligibility.ApprovalReference)).AsString(255).Nullable()
            .WithColumn(nameof(BnplProductEligibility.PolicyVersion)).AsString(128).Nullable()
            .WithColumn(nameof(BnplProductEligibility.CreatedOnUtc)).AsNopDateTime2().NotNullable()
            .WithColumn(nameof(BnplProductEligibility.UpdatedOnUtc)).AsNopDateTime2().NotNullable();
    }
}
