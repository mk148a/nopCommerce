using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Video service interface
    /// </summary>
    public partial interface IProductReviewsEtsyReviewService
    {

        /// <summary>
        /// Deletes a EtsyReview
        /// </summary>
        /// <param name="EtsyReview">Video</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteEtsyReviewAsync(EtsyReview EtsyReview);



        /// </returns>
        Task<EtsyReview> InsertEtsyReviewAsync(long? shopId, long transactionId,
            long? listingId, long? buyerUserId, int? rating, string review, string language, string imageUrlFullxfull,
            long? createTimestamp, long? createdTimestamp, long? updateTimestamp, long? updatedTimestam,string sku);


        /// <summary>
        /// Updates the EtsyReview
        /// </summary>
        /// <param name="videoId">The EtsyReview identifier</param>
        /// <param name="videoBinary">The EtsyReview binary</param>
        /// <param name="mimeType">The EtsyReview MIME type</param>
        /// <param name="seoFilename">The SEO filename</param>
        /// <param name="altAttribute">"alt" attribute for "img" HTML element</param>
        /// <param name="titleAttribute">"title" attribute for "img" HTML element</param>
        /// <param name="isNew">A value indicating whether the EtsyReview is new</param>
        /// <param name="validateBinary">A value indicating whether to validated provided EtsyReview binary</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the EtsyReview
        /// </returns>
        Task<EtsyReview> UpdateEtsyReviewAsync(int EtsyReviewId, long? shopId, long transactionId,
            long? listingId, long? buyerUserId, int? rating, string review, string language, string imageUrlFullxfull,
            long? createTimestamp, long? createdTimestamp, long? updateTimestamp, long? updatedTimestamp);

        /// <summary>
        /// Updates the EtsyReview
        /// </summary>
   
        Task<EtsyReview> UpdateEtsyReviewAsync(EtsyReview EtsyReview);

        Task<EtsyReview> GetEtsyReviewByIdAsync(int EtsyReviewId);
        Task<List<EtsyReview>> GetEtsyReviewByTransactionIdAsync(long transactionId);

        Task<bool> AskEtsyReviewByTransactionIdAsync(long transactionId);

        Task<IList<EtsyReview>> GetEtsyReviews();





    }
}