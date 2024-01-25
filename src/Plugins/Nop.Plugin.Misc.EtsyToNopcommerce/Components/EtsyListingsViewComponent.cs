using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.EtsyToNopcommerce.Models.Etsy.Listings;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Components
{
    [ViewComponent(Name = "EtsyListings")]
    public class EtsyListingsViewComponent : ViewComponent
    {
        #region Fields

        private readonly IEtsyListingsService _etsyListingsService;
        [BindProperty] private IList<EtsyListing> EtsyListings { get; set; }

        #endregion Fields

        #region Ctor

        public EtsyListingsViewComponent(IEtsyListingsService etsyListingsService)
        {
            _etsyListingsService = etsyListingsService;
        }

        #endregion Ctor

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the view component result
        /// </returns>
        public async Task<IViewComponentResult> InvokeAsync()
        {
            EtsyListings = await _etsyListingsService.GetEtsyListings();

            return View(
                "~/Plugins/Nop.Plugin.Misc.EtsyToNopcommerce/Views/Shared/Components/EtsyListings/Default.cshtml",
                EtsyListings);
        }

        #endregion Methods
    }
}