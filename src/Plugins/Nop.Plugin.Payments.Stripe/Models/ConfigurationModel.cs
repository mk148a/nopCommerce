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

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TestPublishableKey")]
        public string TestPublishableKey { get; set; }
        public bool TestPublishableKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TestSecretKey")]
        [DataType(DataType.Password)]
        public string TestSecretKey { get; set; }
        public bool TestSecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookSecret")]
        [DataType(DataType.Password)]
        public string WebhookSecret { get; set; }

        public bool WebhookSecretConfigured { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookEndpointId")]
        public string WebhookEndpointId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.WebhookEndpointUrl")]
        public string WebhookEndpointUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TestWebhookSecret")]
        [DataType(DataType.Password)]
        public string TestWebhookSecret { get; set; }

        public bool TestWebhookSecretConfigured { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TestWebhookEndpointId")]
        public string TestWebhookEndpointId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TestWebhookEndpointUrl")]
        public string TestWebhookEndpointUrl { get; set; }

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
