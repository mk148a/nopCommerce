using System;
using System.Linq;
using Microsoft.AspNetCore.Html;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;
using Nop.Services.Catalog;
using Nop.Web.Factories;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core;
using System.Collections.Generic;
using LinqToDB.Common;
using Nop.Plugin.Misc.EtsyToNopcommerce.Domains;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Plugin.Misc.EtsyToNopcommerce.Services;
using EtsyReview = Nop.Plugin.Misc.EtsyToNopcommerce.Domains.EtsyReview;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Components
{
    [ViewComponent(Name = "EtsyReviews")]
    public class EtsyReviewsViewComponent : ViewComponent
    {

        #region Fields

    
        private readonly IProductReviewsEtsyReviewService _etsyReviewService;
        [BindProperty] 
        IList<EtsyReview> reviews { get; set; }

        #endregion

        #region Ctor

        public EtsyReviewsViewComponent(IProductReviewsEtsyReviewService etsyReviewService)
        {
            _etsyReviewService= etsyReviewService;
        }

        #endregion

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

            reviews = await _etsyReviewService.GetEtsyReviews();

            return View("~/Plugins/Nop.Plugin.Misc.EtsyToNopcommerce/Views/Shared/Components/EtsyReviews/Default.cshtml", reviews);




        }

        #endregion
    }
}
