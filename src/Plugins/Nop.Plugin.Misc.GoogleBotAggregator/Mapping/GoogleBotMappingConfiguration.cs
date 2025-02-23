using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.GoogleBotAggregator.Domain;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Mapping;

/// <summary>
/// Represents Google Bot entity builder
/// </summary>
public class GoogleBotMappingConfiguration : NopEntityBuilder<GoogleBotCustomer>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(GoogleBotCustomer.CustomerId)).AsInt32().ForeignKey<Customer>()
            .WithColumn(nameof(GoogleBotCustomer.UserAgent)).AsString(1000).NotNullable()
            .WithColumn(nameof(GoogleBotCustomer.IpAddress)).AsString(50).NotNullable()
            .WithColumn(nameof(GoogleBotCustomer.CreatedOnUtc)).AsDateTime2().NotNullable();
    }
} 