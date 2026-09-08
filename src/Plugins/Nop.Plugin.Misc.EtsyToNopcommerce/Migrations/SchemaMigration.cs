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
    [NopMigration("2023/12/31 10:16:55:1687541", "Nop.Plugin.Misc.EtsyToNopcommerce.Migrations update EtsyCustomers Test collumn deleted", UpdateMigrationType.Data)]
   //sample [NopMigration("2022/01/01 12:00:00:2551770", "Category. Add some new property", UpdateMigrationType.Data, MigrationProcessType.Update)]
    public class SchemaMigration : Migration
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
                    var etsyReviewTableIsExist = Schema.Table("EtsyReview").Exists();
                    if (!etsyReviewTableIsExist)
                    {
                        Create.TableFor<EtsyReview>();
                    }
                    else
                    {
                        try
                        {

                       
                        Alter.Table("EtsyReview");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                  
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                  
                }
                try
                {
                    var productReviewsTransactionsMappingTableIsExist = Schema.Table("ProductReviewsTransactionsMapping").Exists();
                    if (!productReviewsTransactionsMappingTableIsExist)
                    {
                        Create.TableFor<ProductReviewsTransactionsMapping>();
                    }
                    else
                    {
                        try
                        {


                            Alter.Table("ProductReviewsTransactionsMapping");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                   
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    
                }
                try
                {

                    var etsyListingTableIsExist = Schema.Table("EtsyListing").Exists();
                    if (!etsyListingTableIsExist)
                    {
                        Create.TableFor<EtsyListing>();
                    }
                    else
                    {
                        try
                        {


                            Alter.Table("EtsyListing");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                   
              
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);

                }
                try
                {


                    var etsyCustomerTableIsExist = Schema.Table("EtsyCustomer").Exists();
                    if (!etsyCustomerTableIsExist)
                    {
                        Create.TableFor<EtsyCustomer>();
                    }
                    else
                    {
                        try
                        {
                            Delete
                                .Column("Test")
                                .FromTable("EtsyCustomer");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
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
