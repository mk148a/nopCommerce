using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.Collections.Generic;

namespace Nop.Plugin.Misc.EtsyToNopcommerce.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopName")]
        public string ShopName { get; set; }
        public bool ShopName_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopId")]
        public int? ShopId { get; set; }
        public bool ShopId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RequestUrl")]
        public string RequestUrl { get; set; }
        public bool RequestUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RequestAccessTokenUrl")]
        public string RequestAccessTokenUrl { get; set; }
        public bool RequestAccessTokenUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerKey")]
        public string ConsumerKey { get; set; }
        public bool ConsumerKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerSecret")]
        public string ConsumerSecret { get; set; }
        public bool ConsumerSecret_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.Token")]
        public string Token { get; set; }
        public bool Token_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenSecret")]
        public string TokenSecret { get; set; }
        public bool TokenSecret_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RefreshToken")]
        public string RefreshToken { get; set; }
        public bool RefreshToken_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenDate")]
        public DateTime? TokenDate { get; set; }
        public bool TokenDate_OverrideForStore { get; set; }


    }
}