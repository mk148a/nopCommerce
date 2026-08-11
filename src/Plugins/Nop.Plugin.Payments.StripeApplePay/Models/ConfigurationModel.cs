using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.StripeApplePay.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }


        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.PublishableKey")]
        public string PublishableKey { get; set; }
        public bool PublishableKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.SecretKey")]
        public string SecretKey { get; set; }
        public bool SecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.TestPublishableKey")]
        public string TestPublishableKey { get; set; }
        public bool TestPublishableKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.TestSecretKey")]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string TestSecretKey { get; set; }
        public bool TestSecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.StripeApplePay.Fields.IsCardStorage")]
        public bool IsCardStorage { get; set; }
        

     
    }
}
