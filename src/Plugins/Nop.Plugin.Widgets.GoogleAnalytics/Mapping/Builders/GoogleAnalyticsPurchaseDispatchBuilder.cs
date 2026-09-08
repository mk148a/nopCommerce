using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.GoogleAnalytics.Domains;

namespace Nop.Plugin.Widgets.GoogleAnalytics.Mapping.Builders;

public class GoogleAnalyticsPurchaseDispatchBuilder : NopEntityBuilder<GoogleAnalyticsPurchaseDispatch>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.OrderId)).AsInt32().NotNullable()
            .WithColumn(nameof(GoogleAnalyticsPurchaseDispatch.CreatedOnUtc)).AsNopDateTime2().NotNullable();
    }
}
