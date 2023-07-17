using System;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Widgets.CustomProductReviews.Mapping.Builders;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Migrations
{
    [NopMigration("2022/12/12 15:40:55:1687541", "Nop.Plugin.Misc.EtsyToNopcommerce.Migrations base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : FluentMigrator.Migration
    {
        private readonly IMigrationManager _migrationManager;

        public SchemaMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up() {
            try
            {
                Create.TableFor<EtsyReview>();
                Create.TableFor<ProductReviewsTransactionsMapping>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }     
        public override void Down()
        {
            try
            {

            //Delete.Table("EtsyReview");
            //Delete.Table("ProductReviewsTransactionsMapping");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                
            }


        }
    }
}
