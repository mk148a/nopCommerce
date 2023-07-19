using System;
using System.ComponentModel.DataAnnotations;
using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.EtsyToNopcommerce
{
    /// <summary>
    /// Represents a EcbExchangeRate plugin settings
    /// </summary>
    public class EtsyToNopcommerceSettings: ISettings
    {
        /// <summary>
        /// Link to ECB exchange xml data
        /// </summary>
       
        public string ShopName { get; set; }
        public int? ShopId { get; set; }
        public string RequestUrl { get; set; }
        public string RequestAccessTokenUrl { get; set; }
        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
        public string Token { get; set; }
        public string TokenSecret { get; set; }
       
        public string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }

        [Display(Name = "Tarih")]
        public DateTime? TokenDate { get; set; }
    }
}