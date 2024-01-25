using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// Product Reviews Transactions Mapping service
    /// </summary>
    public partial class EtsyCustomersService : IEtsyCustomersService
    {
        #region Fields

        private readonly IRepository<EtsyCustomer> _repository;
       
        #endregion

        #region Ctor

        public EtsyCustomersService(IRepository<EtsyCustomer> repository)
        {
            _repository = repository;
           
        }

        #endregion




        #region CRUD methods


        /// Inserts a EtsyCustomer
        public virtual async Task<EtsyCustomer> InsertEtsyCustomerAsync(EtsyCustomer etsyCustomer)
        {



            await _repository.InsertAsync(etsyCustomer);

            return etsyCustomer;
        }


        public virtual async Task<EtsyCustomer> UpdateEtsyCustomerAsync(EtsyCustomer etsyCustomer)
        {

            var findEtsyCustomer = await GetCustomerByBuyerUserIdAsync(etsyCustomer.BuyerUserId);
            if (etsyCustomer == null)
                return null;
            
            etsyCustomer.BuyerUserId = findEtsyCustomer.BuyerUserId;

            await _repository.UpdateAsync(etsyCustomer);

            return etsyCustomer;
        }

        /// Deletes a EtsyCustomer
        public virtual async Task DeleteEtsyCustomerAsync(EtsyCustomer etsyCustomer)
        {
            if (etsyCustomer == null)
                throw new ArgumentNullException(nameof(etsyCustomer));

            //delete from database
            await _repository.DeleteAsync(etsyCustomer);
        }




        public virtual async Task<IList<EtsyCustomer>> GetEtsyCustomers()
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
        public virtual async Task<EtsyCustomer> GetCustomerByBuyerUserIdAsync(long BuyerUserId)
        {
            if (BuyerUserId == 0)
                return new  EtsyCustomer();

            var query = from p in _repository.Table
                where p.BuyerUserId == BuyerUserId
                        select p;

            var mappings = await query.ToListAsync();

            return mappings.FirstOrDefault();
        }

        public virtual async Task<bool> AskEtsyCustomerByBuyerUserIdAsync(long BuyerUserId)
        {
            if (BuyerUserId == 0)
                return false;

            var result =await _repository.Table.AnyAsync(x => x.BuyerUserId == BuyerUserId);

           

            return result;
        }

        #endregion
    }
}
