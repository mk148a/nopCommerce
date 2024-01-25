using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Video service interface
    /// </summary>
    public partial interface IEtsyCustomersService
    {

        /// <summary>
        /// Deletes a IEtsyCustomersService
        /// </summary>
        /// <param name="EtsyCustomer">Video</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task DeleteEtsyCustomerAsync(EtsyCustomer etsyCustomer);


        /// </returns>
        Task<EtsyCustomer> InsertEtsyCustomerAsync(EtsyCustomer etsyCustomer);

   
        Task<EtsyCustomer> UpdateEtsyCustomerAsync(EtsyCustomer etsyCustomer);

        Task<EtsyCustomer> GetCustomerByBuyerUserIdAsync(long BuyerUserId);

        Task<bool> AskEtsyCustomerByBuyerUserIdAsync(long BuyerUserId);

        Task<IList<EtsyCustomer>> GetEtsyCustomers();





    }
}