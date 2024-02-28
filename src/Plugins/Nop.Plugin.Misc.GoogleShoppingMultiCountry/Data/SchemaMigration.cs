using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.GoogleShoppingMultiCountry.Domains;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Data
{
    [NopMigration("2024/01/01 12:00:00", "Feed.GoogleShopping base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : AutoReversingMigration
    {
        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            Create.TableFor<GoogleFeedProductRecord>();
            Create.TableFor<GoogleTaxonomyRecord>();

        }
   

        #endregion
    }
}
