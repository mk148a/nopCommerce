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
    public partial class ProductReviewsEtsyReviewService : IProductReviewsEtsyReviewService
    {
        #region Fields

        private readonly IRepository<EtsyReview> _repository;
       
        #endregion

        #region Ctor

        public ProductReviewsEtsyReviewService(IRepository<EtsyReview> repository)
        {
            _repository = repository;
           
        }

        #endregion




        #region CRUD methods

        /// <summary>
        /// Inserts a EtsyReview
        /// </summary>
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
        public virtual async Task<EtsyReview> InsertEtsyReviewAsync(long? shopId, long transactionId,
            long? listingId, long? buyerUserId, int? rating, string review, string language, string imageUrlFullxfull,
            long? createTimestamp, long? createdTimestamp, long? updateTimestamp, long? updatedTimestamp,string sku)
        {


            var EtsyReview = new EtsyReview
            {
                ShopId = shopId,
                TransactionId = transactionId,
                ListingId = listingId,
                BuyerUserId = buyerUserId,
                Rating = rating,
                Review = review,
                Language = language,
                ImageUrlFullxfull = imageUrlFullxfull,
                CreateTimestamp = createTimestamp,
                CreatedTimestamp = createdTimestamp,
                UpdateTimestamp = updateTimestamp,
                UpdatedTimestamp = updatedTimestamp,
                Sku = sku
            };

            await _repository.InsertAsync(EtsyReview);

            return EtsyReview;
        }

        /// <summary>
        /// Updates the EtsyReview
        /// </summary>
        /// <param name="EtsyReviewId">The EtsyReview identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the EtsyReview
        /// </returns>
        public virtual async Task<EtsyReview> UpdateEtsyReviewAsync(int EtsyReviewId, long? shopId, long transactionId,
            long? listingId, long? buyerUserId, int? rating, string review, string language, string imageUrlFullxfull,
            long? createTimestamp, long? createdTimestamp, long? updateTimestamp, long? updatedTimestamp)
        {

            var EtsyReview = await GetEtsyReviewByIdAsync(EtsyReviewId);
            if (EtsyReview == null)
                return null;


            EtsyReview.TransactionId = transactionId;
            EtsyReview.ShopId = shopId;
            EtsyReview.TransactionId = transactionId;
            EtsyReview.ListingId= listingId;
            EtsyReview.BuyerUserId= buyerUserId;
            EtsyReview.Rating = rating;
            EtsyReview.Review = review;
            EtsyReview.Language= language;
            EtsyReview.ImageUrlFullxfull= imageUrlFullxfull;
            EtsyReview.CreateTimestamp = createTimestamp;
            EtsyReview.CreatedTimestamp= createdTimestamp;
            EtsyReview.UpdateTimestamp = updateTimestamp;
            EtsyReview.UpdatedTimestamp = updatedTimestamp;

            await _repository.UpdateAsync(EtsyReview);

            return EtsyReview;
        }

        /// <summary>
        /// Updates the EtsyReview
        /// </summary>
        /// <param name="EtsyReview">The EtsyReview to update</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the EtsyReview
        /// </returns>
        public virtual async Task<EtsyReview> UpdateEtsyReviewAsync(EtsyReview EtsyReview)
        {
            if (EtsyReview == null)
                return null;

            await _repository.UpdateAsync(EtsyReview);

            return EtsyReview;
        }

        /// <summary>
        /// Deletes a EtsyReview
        /// </summary>
        /// <param name="EtsyReview">EtsyReview</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteEtsyReviewAsync(EtsyReview EtsyReview)
        {
            if (EtsyReview == null)
                throw new ArgumentNullException(nameof(EtsyReview));

            //delete from database
            await _repository.DeleteAsync(EtsyReview);
        }


        /// <summary>
        /// Gets a EtsyReview
        /// </summary>
        /// <param name="EtsyReviewId">EtsyReview identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the EtsyReview
        /// </returns>
        public virtual async Task<EtsyReview> GetEtsyReviewByIdAsync(int EtsyReviewId)
        {
            return await _repository.GetByIdAsync(EtsyReviewId, cache => default);
        }


        public virtual async Task<IList<EtsyReview>> GetEtsyReviews()
        {
            var dd=(await _repository.GetAllAsync(query=>query.Where(x=>x!=null)));
            return  dd;
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
        public virtual async Task<List<EtsyReview>> GetEtsyReviewByTransactionIdAsync(long transactionId)
        {
            if (transactionId == 0)
                return new  List<EtsyReview>();

            var query = from p in _repository.Table
                where p.TransactionId == transactionId
                        select p;

            var mappings = await query.ToListAsync();

            return mappings;
        }

        public virtual async Task<bool> AskEtsyReviewByTransactionIdAsync(long transactionId)
        {
            if (transactionId == 0)
                return false;

            var result =await _repository.Table.AnyAsync(x => x.TransactionId == transactionId);

           

            return result;
        }

        #endregion
    }
}
