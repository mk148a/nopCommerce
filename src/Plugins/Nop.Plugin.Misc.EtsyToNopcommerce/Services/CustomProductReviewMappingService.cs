using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Nop.Data;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;


namespace Nop.Plugin.Misc.EtsyToNopcommerce.Services
{
    /// <summary>
    /// CustomProductReviewMapping service
    /// </summary>
    public partial class CustomProductReviewMappingService : ICustomProductReviewMappingService
    {
        #region Fields

        private readonly IRepository<CustomProductReviewMapping> _customProductReviewMappingRepository;
        //private readonly IRepository<ProductCustomProductReviewMapping> _productCustomProductReviewMappingRepository;

        #endregion

        #region Ctor

        public CustomProductReviewMappingService(IRepository<CustomProductReviewMapping> customProductReviewMappingRepository)
        {
            _customProductReviewMappingRepository = customProductReviewMappingRepository;
        }

        #endregion




        #region CRUD methods

        /// <summary>
        /// Inserts a customProductReviewMapping
        /// </summary>
        /// <param name="videoBinary">The customProductReviewMapping binary</param>
        /// <param name="mimeType">The customProductReviewMapping MIME type</param>
        /// <param name="seoFilename">The SEO filename</param>
        /// <param name="altAttribute">"alt" attribute for "img" HTML element</param>
        /// <param name="titleAttribute">"title" attribute for "img" HTML element</param>
        /// <param name="isNew">A value indicating whether the customProductReviewMapping is new</param>
        /// <param name="validateBinary">A value indicating whether to validated provided customProductReviewMapping binary</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the customProductReviewMapping
        /// </returns>
        public virtual async Task<CustomProductReviewMapping> InsertCustomProductReviewMappingAsync(int productReviewId, int? pictureId, int? videoIdId)
        {


            var customProductReviewMapping = new CustomProductReviewMapping
            {
                ProductReviewId = productReviewId,
                PictureId = pictureId,
                VideoId = videoIdId
            };

            await _customProductReviewMappingRepository.InsertAsync(customProductReviewMapping);

            return customProductReviewMapping;
        }

        /// <summary>
        /// Updates the customProductReviewMapping
        /// </summary>
        /// <param name="customProductReviewMappingId">The customProductReviewMapping identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the customProductReviewMapping
        /// </returns>
        public virtual async Task<CustomProductReviewMapping> UpdateCustomProductReviewMappingAsync(int customProductReviewMappingId, int productReviewId, int pictureId, int videoIdId)
        {

            var customProductReviewMapping = await GetCustomProductReviewMappingByIdAsync(customProductReviewMappingId);
            if (customProductReviewMapping == null)
                return null;


            customProductReviewMapping.PictureId = pictureId;
            customProductReviewMapping.VideoId = videoIdId;
            customProductReviewMapping.ProductReviewId = productReviewId;

            await _customProductReviewMappingRepository.UpdateAsync(customProductReviewMapping);

            return customProductReviewMapping;
        }

        /// <summary>
        /// Updates the customProductReviewMapping
        /// </summary>
        /// <param name="customProductReviewMapping">The customProductReviewMapping to update</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the customProductReviewMapping
        /// </returns>
        public virtual async Task<CustomProductReviewMapping> UpdateCustomProductReviewMappingAsync(CustomProductReviewMapping customProductReviewMapping)
        {
            if (customProductReviewMapping == null)
                return null;

            await _customProductReviewMappingRepository.UpdateAsync(customProductReviewMapping);

            return customProductReviewMapping;
        }

        /// <summary>
        /// Deletes a customProductReviewMapping
        /// </summary>
        /// <param name="customProductReviewMapping">CustomProductReviewMapping</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task DeleteCustomProductReviewMappingAsync(CustomProductReviewMapping customProductReviewMapping)
        {
            if (customProductReviewMapping == null)
                throw new ArgumentNullException(nameof(customProductReviewMapping));

            //delete from database
            await _customProductReviewMappingRepository.DeleteAsync(customProductReviewMapping);
        }


        /// <summary>
        /// Gets a customProductReviewMapping
        /// </summary>
        /// <param name="customProductReviewMappingId">CustomProductReviewMapping identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the customProductReviewMapping
        /// </returns>
        public virtual async Task<CustomProductReviewMapping> GetCustomProductReviewMappingByIdAsync(int customProductReviewMappingId)
        {
            return await _customProductReviewMappingRepository.GetByIdAsync(customProductReviewMappingId, cache => default);
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
        public virtual async Task<List<CustomProductReviewMapping>> GetCustomProductReviewMappingByProductReviewIdAsync(int productReviewId)
        {
            if (productReviewId == 0)
                return new  List<CustomProductReviewMapping>();

            var query = from p in _customProductReviewMappingRepository.Table
                where p.ProductReviewId == productReviewId
                select p;

            var mappings = await query.ToListAsync();

            return mappings;
        }
        


        #endregion
    }
}
