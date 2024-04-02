using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.GoogleShoppingMultiCountry.Models
{
    public record GoogleShoppingMultiCountryModel
    {
        public GoogleShoppingMultiCountryModel()
        {
            AvailableGoogleCategories = new List<SelectListItem>();
            GeneratedFiles = new List<GeneratedFileModel>();
            GoogleFeedProductSearchModel = new GoogleFeedProductSearchModel();
        }

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.ProductPictureSize")]
        public int ProductPictureSize { get; set; }
        public bool ProductPictureSize_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.DefaultGoogleCategory")]
        public string DefaultGoogleCategory { get; set; }
        public string DefaultGoogleCategoryId { get; set; }
        public IList<SelectListItem> AvailableGoogleCategories { get; set; }
        public bool DefaultGoogleCategory_OverrideForStore { get; set; }
        public bool DefaultGoogleCategoryId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoWeight")]
        public bool PassShippingInfoWeight { get; set; }
        public bool PassShippingInfoWeight_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.PassShippingInfoDimensions")]
        public bool PassShippingInfoDimensions { get; set; }
        public bool PassShippingInfoDimensions_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.PricesConsiderPromotions")]
        public bool PricesConsiderPromotions { get; set; }
        public bool PricesConsiderPromotions_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.GoogleShoppingMultiCountry.StaticFilePath")]
        public IList<GeneratedFileModel> GeneratedFiles { get; set; }

        public bool HideGeneralBlock { get; set; }

        public bool HideProductSettingsBlock { get; set; }

        public GoogleFeedProductSearchModel GoogleFeedProductSearchModel { get; set; }
    }
}
