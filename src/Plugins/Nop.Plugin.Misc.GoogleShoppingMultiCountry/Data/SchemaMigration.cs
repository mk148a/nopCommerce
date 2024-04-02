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
    [NopMigration("2024/03/02 12:00:00", "Misc.GoogleShoppingMultiCountry base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : AutoReversingMigration
    {
        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            try
            {

                Create.TableFor<GoogleFeedProductRecord>();
            }
            catch (Exception e)
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + e);

            }
            finally
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + "GoogleFeedProductRecord table is created");
            }
            try
            {
                Create.TableFor<GoogleTaxonomyRecord>();
            }
            catch (Exception e)
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + e);

            }
            finally
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + "GoogleTaxonomyRecord table is created");
            }
            try
            {
                Create.TableFor<CategoryGoogleTaxonomyRecordMapping>();
            }
            catch (Exception e)
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + e);

            }
            finally
            {
                Console.WriteLine("GoogleShoppingMultiCountry " + "CategoryGoogleTaxonomyRecordMapping table is created");
            }

        }

   

        #endregion
    }
}
