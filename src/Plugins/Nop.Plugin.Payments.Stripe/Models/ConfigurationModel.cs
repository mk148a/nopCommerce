using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.Stripe.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }


        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.PublishableKey")]
        public string PublishableKey { get; set; }
        public bool PublishableKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.SecretKey")]
        public string SecretKey { get; set; }
        public bool SecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookSecret")]
        [DataType(DataType.Password)]
        public string WebhookSecret { get; set; }

        public bool WebhookSecretConfigured { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookEndpointId")]
        public string WebhookEndpointId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookEndpointUrl")]
        public string WebhookEndpointUrl { get; set; }

        public string WebhookEndpointStatus { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.IsCardStorage")]
        public bool IsCardStorage { get; set; }
        public bool IsCardStorage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.PaymentSuccessUrl")]
        public string PaymentSuccessUrl { get; set; }
        public bool PaymentSuccessUrl_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.PaymentErrorUrl")]
        public string PaymentErrorUrl { get; set; }
        public bool PaymentErrorUrl_OverrideForStore { get; set; }

     
    }
}
