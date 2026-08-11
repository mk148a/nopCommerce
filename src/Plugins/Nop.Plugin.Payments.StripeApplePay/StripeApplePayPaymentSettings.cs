using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.StripeApplePay
{
    public class StripeApplePayPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets OAuth2 application identifier
        /// </summary>
        public string SecretKey { get; set; }
        public string PublishableKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Stripe test mode is active.
        /// The default is false so existing stores continue to use live keys.
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Gets or sets the Stripe test-mode publishable key.
        /// </summary>
        public string TestPublishableKey { get; set; }

        /// <summary>
        /// Gets or sets the Stripe test-mode secret key.
        /// </summary>
        public string TestSecretKey { get; set; }
        
        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        public string GetActiveSecretKey() => UseSandbox ? TestSecretKey : SecretKey;

        public string GetActivePublishableKey() => UseSandbox ? TestPublishableKey : PublishableKey;
    }
}
