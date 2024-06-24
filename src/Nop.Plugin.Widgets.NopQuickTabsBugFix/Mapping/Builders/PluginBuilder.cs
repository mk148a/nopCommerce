using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.NopQuickTabsBugFix.Domains;

namespace Nop.Plugin.Widgets.NopQuickTabsBugFix.Mapping.Builders
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