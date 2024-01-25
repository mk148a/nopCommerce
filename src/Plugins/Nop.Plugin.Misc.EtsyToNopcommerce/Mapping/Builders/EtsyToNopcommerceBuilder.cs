using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Catalog;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Mapping.Builders
{
    public class EtsyToNopcommerceBuilder : NopEntityBuilder<ProductReviewsTransactionsMapping>
    {
        #region Methods

        public override void MapEntity(CreateTableExpressionBuilder table)
        {
           
            table
                .WithColumn(nameof(ProductReviewsTransactionsMapping.Id)).AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn(nameof(ProductReviewsTransactionsMapping.ProductReviewId)).AsInt32().ForeignKey<ProductReview>()
                .WithColumn(nameof(ProductReviewsTransactionsMapping.EtsyReviewId)).AsInt32().ForeignKey<EtsyReview>();

               

        }

        #endregion
    }




  
}