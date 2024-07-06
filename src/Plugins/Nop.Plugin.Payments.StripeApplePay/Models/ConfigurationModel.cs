using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.StripeApplePay.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }


        [NopResourceDisplayName("Nop.Plugin.Payments.StripeApplePay.Fields.PublishableKey")]
        public string PublishableKey { get; set; }
        public bool PublishableKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Payments.StripeApplePay.Fields.SecretKey")]
        public string SecretKey { get; set; }
        public bool SecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Payments.StripeApplePay.Fields.IsCardStorage")]
        public bool IsCardStorage { get; set; }
        public bool IsCardStorage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Payments.StripeApplePay.Fields.PaymentSuccessUrl")]
        public string PaymentSuccessUrl { get; set; }
        public bool PaymentSuccessUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Nop.Plugin.Payments.StripeApplePay.Fields.PaymentErrorUrl")]
        public string PaymentErrorUrl { get; set; }
        public bool PaymentErrorUrl_OverrideForStore { get; set; }

     
    }
}