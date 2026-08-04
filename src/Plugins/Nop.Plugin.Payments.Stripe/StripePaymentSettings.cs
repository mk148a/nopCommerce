using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Stripe
{
    public class StripePaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets OAuth2 application identifier
        /// </summary>
        public string SecretKey { get; set; }
        public string PublishableKey { get; set; }

        /// <summary>
        /// Gets or sets the signing secret for the Stripe webhook endpoint.
        /// This is intentionally separate from the Stripe API secret key.
        /// </summary>
        public string WebhookSecret { get; set; }

        /// <summary>
        /// Gets or sets the Stripe webhook endpoint identifier managed by the plugin.
        /// </summary>
        public string WebhookEndpointId { get; set; }

        /// <summary>
        /// Gets or sets the last URL synchronized with Stripe.
        /// </summary>
        public string WebhookEndpointUrl { get; set; }
        
        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
