using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Catalog;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using EtsyReviewEntity = Nop.Plugin.Misc.EtsyToNopcommerce.Domains.EtsyReview;
using ProductReviewsTransactionsMappingEntity = Nop.Plugin.Misc.EtsyToNopcommerce.Domains.ProductReviewsTransactionsMapping;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Mapping.Builders
{
    public class EtsyToNopcommerceBuilder : NopEntityBuilder<ProductReviewsTransactionsMappingEntity>
    {
        #region Methods

        public override void MapEntity(CreateTableExpressionBuilder table)
        {
           
            table
                .WithColumn(nameof(ProductReviewsTransactionsMappingEntity.Id)).AsInt32().NotNullable().PrimaryKey().Identity()
                .WithColumn(nameof(ProductReviewsTransactionsMappingEntity.ProductReviewId)).AsInt32().ForeignKey<ProductReview>()
                .WithColumn(nameof(ProductReviewsTransactionsMappingEntity.EtsyReviewId)).AsInt32().ForeignKey<EtsyReviewEntity>();

               

        }

        #endregion
    }




  
}
