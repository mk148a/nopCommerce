using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Product Reviews Transactions Mapping service
    /// </summary>
    public partial class EtsyListingsService : IEtsyListingsService
    {
        #region Fields

        private readonly IRepository<EtsyListing> _repository;
       
        #endregion

        #region Ctor

        public EtsyListingsService(IRepository<EtsyListing> repository)
        {
            _repository = repository;
           
        }

        #endregion




        #region CRUD methods


        /// Inserts a EtsyListing
        public virtual async Task<EtsyListing> InsertEtsyListingAsync(EtsyListing etsyListing)
        {



            await _repository.InsertAsync(etsyListing);

            return etsyListing;
        }

        /// <summary>
        /// Updates the EtsyReview
        /// </summary>
        /// <param name="EtsyReviewId">The EtsyReview identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the EtsyReview
        /// </returns>
        public virtual async Task<EtsyListing> UpdateEtsyListingAsync(EtsyListing etsyListing)
        {

            var findEtsyListing = await GetListingByIdAsync(etsyListing.ListingId);
            if (etsyListing == null)
                return null;
            
            etsyListing.ListingId = findEtsyListing.ListingId;

            await _repository.UpdateAsync(etsyListing);

            return etsyListing;
        }

        /// <summary>
        /// Deletes a EtsyReview
        /// </summary>
        /// <param name="EtsyReview">EtsyReview</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteEtsyListingAsync(EtsyListing etsyListing)
        {
            if (etsyListing == null)
                throw new ArgumentNullException(nameof(etsyListing));

            //delete from database
            await _repository.DeleteAsync(etsyListing);
        }




        public virtual async Task<IList<EtsyListing>> GetEtsyListings()
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
        public virtual async Task<EtsyListing> GetListingByIdAsync(long Id)
        {
            if (Id == 0)
                return new  EtsyListing();

            var query = from p in _repository.Table
                where p.ListingId == Id
                        select p;

            var mappings = await query.ToListAsync();

            return mappings.FirstOrDefault();
        }

        public virtual async Task<bool> AskEtsyListingByIdAsync(long Id)
        {
            if (Id == 0)
                return false;

            var result =await _repository.Table.AnyAsync(x => x.ListingId == Id);

           

            return result;
        }

        #endregion
    }
}
