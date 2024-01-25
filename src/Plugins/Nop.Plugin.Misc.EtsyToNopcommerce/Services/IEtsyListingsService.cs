using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Video service interface
    /// </summary>
    public partial interface IEtsyListingsService
    {

        /// <summary>
        /// Deletes a EtsyReview
        /// </summary>
        /// <param name="EtsyReview">Video</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteEtsyListingAsync(EtsyListing etsyListing);


        /// </returns>
        Task<EtsyListing> InsertEtsyListingAsync(EtsyListing etsyListing);

   
        Task<EtsyListing> UpdateEtsyListingAsync(EtsyListing EtsyReview);

        Task<EtsyListing> GetListingByIdAsync(long Id);

        Task<bool> AskEtsyListingByIdAsync(long Id);

        Task<IList<EtsyListing>> GetEtsyListings();





    }
}