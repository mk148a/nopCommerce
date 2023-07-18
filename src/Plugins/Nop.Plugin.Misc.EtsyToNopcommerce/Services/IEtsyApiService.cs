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
        
        Task<string> GetAllEtsyReviews();

        Task<HashSet<Receipt>> GetAllEtsyReceipts();





    }
}