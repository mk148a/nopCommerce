using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Video service interface
    /// </summary>
    public partial interface IEtsyApiService
    {
        
        Task<string> GetAllEtsyReviews(bool onlyAddNopcommerceProduct = true);

        Task<HashSet<Receipt>> GetAllEtsyReceipts(bool onlyAddNopcommerceProduc=true);

        Task<string> ListingleriAlVeIsle();

        Task<string> GetAllEtsyCustomers();



    }
}