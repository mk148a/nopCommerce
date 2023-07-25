using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Product Reviews Transactions Mapping service
    /// </summary>
    public partial class ProductReviewsTransactionsMappingService : IProductReviewsTransactionsMappingService
    {
        #region Fields

        private readonly IRepository<ProductReviewsTransactionsMapping> _repository;

        #endregion

        #region Ctor

        public ProductReviewsTransactionsMappingService(IRepository<ProductReviewsTransactionsMapping> repository)
        {
            _repository = repository;
        }

        #endregion




        #region CRUD methods

        /// <summary>
        /// Inserts a ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="videoBinary">The ProductReviewsTransactionsMapping binary</param>
        /// <param name="mimeType">The ProductReviewsTransactionsMapping MIME type</param>
        /// <param name="seoFilename">The SEO filename</param>
        /// <param name="altAttribute">"alt" attribute for "img" HTML element</param>
        /// <param name="titleAttribute">"title" attribute for "img" HTML element</param>
        /// <param name="isNew">A value indicating whether the ProductReviewsTransactionsMapping is new</param>
        /// <param name="validateBinary">A value indicating whether to validated provided ProductReviewsTransactionsMapping binary</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ProductReviewsTransactionsMapping
        /// </returns>
        public virtual async Task<ProductReviewsTransactionsMapping> InsertProductReviewsTransactionsMappingAsync(int productReviewId, long transactionId, int etsyReviewId)
        {


            var ProductReviewsTransactionsMapping = new ProductReviewsTransactionsMapping
            {
                ProductReviewId = productReviewId,
                TransactionId = transactionId,
                EtsyReviewId = etsyReviewId
            };

            await _repository.InsertAsync(ProductReviewsTransactionsMapping);

            return ProductReviewsTransactionsMapping;
        }

        /// <summary>
        /// Updates the ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="ProductReviewsTransactionsMappingId">The ProductReviewsTransactionsMapping identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ProductReviewsTransactionsMapping
        /// </returns>
        public virtual async Task<ProductReviewsTransactionsMapping> UpdateProductReviewsTransactionsMappingAsync(int ProductReviewsTransactionsMappingId, int productReviewId, long transactionId)
        {

            var ProductReviewsTransactionsMapping = await GetProductReviewsTransactionsMappingByIdAsync(ProductReviewsTransactionsMappingId);
            if (ProductReviewsTransactionsMapping == null)
                return null;


            ProductReviewsTransactionsMapping.TransactionId = transactionId;
            ProductReviewsTransactionsMapping.ProductReviewId = productReviewId;

            await _repository.UpdateAsync(ProductReviewsTransactionsMapping);

            return ProductReviewsTransactionsMapping;
        }

        /// <summary>
        /// Updates the ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="ProductReviewsTransactionsMapping">The ProductReviewsTransactionsMapping to update</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ProductReviewsTransactionsMapping
        /// </returns>
        public virtual async Task<ProductReviewsTransactionsMapping> UpdateProductReviewsTransactionsMappingAsync(ProductReviewsTransactionsMapping ProductReviewsTransactionsMapping)
        {
            if (ProductReviewsTransactionsMapping == null)
                return null;

            await _repository.UpdateAsync(ProductReviewsTransactionsMapping);

            return ProductReviewsTransactionsMapping;
        }

        /// <summary>
        /// Deletes a ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="ProductReviewsTransactionsMapping">ProductReviewsTransactionsMapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteProductReviewsTransactionsMappingAsync(ProductReviewsTransactionsMapping ProductReviewsTransactionsMapping)
        {
            if (ProductReviewsTransactionsMapping == null)
                throw new ArgumentNullException(nameof(ProductReviewsTransactionsMapping));

            //delete from database
            await _repository.DeleteAsync(ProductReviewsTransactionsMapping);
        }


        /// <summary>
        /// Gets a ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="ProductReviewsTransactionsMappingId">ProductReviewsTransactionsMapping identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the ProductReviewsTransactionsMapping
        /// </returns>
        public virtual async Task<ProductReviewsTransactionsMapping> GetProductReviewsTransactionsMappingByIdAsync(int ProductReviewsTransactionsMappingId)
        {
            return await _repository.GetByIdAsync(ProductReviewsTransactionsMappingId, cache => default);
        }


     

        /// <summary>
        /// Gets pictures by product identifier
        /// </summary>
        /// <param name="productId">Product identifier</param>
        /// <param name="recordsToReturn">Number of records to return. 0 if you want to get all items</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the pictures
        /// </returns>
        public virtual async Task<List<ProductReviewsTransactionsMapping>> GetProductReviewsTransactionsMappingByProductReviewIdAsync(int productReviewId)
        {
            if (productReviewId == 0)
                return new  List<ProductReviewsTransactionsMapping>();

            var query = from p in _repository.Table
                where p.ProductReviewId == productReviewId
                select p;

            var mappings = await query.ToListAsync();

            return mappings;
        }

        /// <summary>
        /// Gets pictures by product identifier
        /// </summary>
        /// <param name="productId">Product identifier</param>
        /// <param name="recordsToReturn">Number of records to return. 0 if you want to get all items</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the pictures
        /// </returns>
        public virtual async Task<List<ProductReviewsTransactionsMapping>> GetProductReviewsTransactionsMappingByTransactionIdAsync(long transactionId)
        {
            if (transactionId == 0)
                return new List<ProductReviewsTransactionsMapping>();

            var query = from p in _repository.Table
                where p.TransactionId == transactionId
                select p;

            var mappings = await query.ToListAsync();

            return mappings;
        }


        #endregion
    }
}
