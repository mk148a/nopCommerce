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
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Stripe Webhook Secret Key
        /// </summary>
        public string WebhookSecret { get; set; }
        
        /// <summary>
        /// Enable 3D Secure
        /// </summary>
        public bool Enable3DS { get; set; }
    }
}
