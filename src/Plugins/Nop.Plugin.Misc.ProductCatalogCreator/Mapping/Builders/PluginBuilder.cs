using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.ProductCatalogCreator.Domains;

namespace Nop.Plugin.Misc.ProductCatalogCreator.Mapping.Builders
{
    public class PluginBuilder : NopEntityBuilder<CustomTable>
    {
        #region Methods

        public override void MapEntity(CreateTableExpressionBuilder table)
        {
        }

        #endregion
    }
}