using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Video service interface
    /// </summary>
    public partial interface IProductReviewsTransactionsMappingService
    {

        /// <summary>
        /// Deletes a ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="ProductReviewsTransactionsMapping">Video</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteProductReviewsTransactionsMappingAsync(ProductReviewsTransactionsMapping ProductReviewsTransactionsMapping);



        /// </returns>
        Task<ProductReviewsTransactionsMapping> InsertProductReviewsTransactionsMappingAsync(int productReviewId, long transactionId,int etsyReviewId);


        /// <summary>
        /// Updates the ProductReviewsTransactionsMapping
        /// </summary>
        /// <param name="videoId">The ProductReviewsTransactionsMapping identifier</param>
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
        Task<ProductReviewsTransactionsMapping> UpdateProductReviewsTransactionsMappingAsync(int ProductReviewsTransactionsMappingId, int productReviewId, long transactionId);

        /// <summary>
        /// Updates the ProductReviewsTransactionsMapping
        /// </summary>
   
        Task<ProductReviewsTransactionsMapping> UpdateProductReviewsTransactionsMappingAsync(ProductReviewsTransactionsMapping ProductReviewsTransactionsMapping);

        Task<ProductReviewsTransactionsMapping> GetProductReviewsTransactionsMappingByIdAsync(int ProductReviewsTransactionsMappingId);
        Task<List<ProductReviewsTransactionsMapping>> GetProductReviewsTransactionsMappingByProductReviewIdAsync(int productReviewId);

        Task<List<ProductReviewsTransactionsMapping>> GetProductReviewsTransactionsMappingByTransactionIdAsync(
            long transactionId);





    }
}