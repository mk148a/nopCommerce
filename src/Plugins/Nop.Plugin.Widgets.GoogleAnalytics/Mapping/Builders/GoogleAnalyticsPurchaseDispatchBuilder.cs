using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Mapping.Builders;

public class GoogleAnalyticsPurchaseDispatchBuilder : NopEntityBuilder<GoogleAnalyticsPurchaseDispatch>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.Status)).AsString(16).NotNullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.LeaseToken)).AsString(64).Nullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.LeaseExpiresOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.ConfirmedOnUtc)).AsDateTime2().Nullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
}
