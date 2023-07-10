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

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ShopId")]
        public int? ShopId { get; set; }
       

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RequestUrl")]
        public string RequestUrl { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RequestAccessTokenUrl")]
        public string RequestAccessTokenUrl { get; set; }


        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerKey")]
        public string ConsumerKey { get; set; }
        
        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.ConsumerSecret")]
        public string ConsumerSecret { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.Token")]
        public string Token { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenSecret")]
        public string TokenSecret { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.RefreshToken")]
        public string RefreshToken { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Misc.EtsyToNopcommerce.Fields.TokenDate")]
        public DateTime TokenDate { get; set; }


    }
}