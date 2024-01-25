using System;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Migrations
{
    [NopMigration("2023/12/28 10:16:55:1687541", "Nop.Plugin.Misc.EtsyToNopcommerce.Migrations base schema")]
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
                try
                {
                    Create.TableFor<EtsyReview>();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                  
                }
                try
                {
                    Create.TableFor<ProductReviewsTransactionsMapping>();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    
                }
                try
                {
                  
                    Create.TableFor<EtsyListing>();
              
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                }
                try
                {

                    Create.TableFor<EtsyCustomer>();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                }

                //Todo:Can be need maybe create a db for Etsy listings and Etsy customers data
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
                //Delete.Table("ProductReviewsTransactionsMapping");
                //Delete.Table("EtsyReview");
               // Delete.Table("EtsyListing");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


        }
    }
}
