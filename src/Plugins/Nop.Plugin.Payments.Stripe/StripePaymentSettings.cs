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
        /// Gets or sets a value indicating whether Stripe test mode is active.
        /// The default is false so existing stores continue to use their live keys.
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
        /// Test-mode webhook signing secret and endpoint state. These are kept
        /// separate from live values so synchronizing a test endpoint can never
        /// replace the live webhook credentials.
        /// </summary>
        public string TestWebhookSecret { get; set; }
        public string TestWebhookEndpointId { get; set; }
        public string TestWebhookEndpointUrl { get; set; }
        
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

        public string GetActiveWebhookSecret() => UseSandbox ? TestWebhookSecret : WebhookSecret;

        public string GetActiveWebhookEndpointId() => UseSandbox ? TestWebhookEndpointId : WebhookEndpointId;

        public string GetActiveWebhookEndpointUrl() => UseSandbox ? TestWebhookEndpointUrl : WebhookEndpointUrl;

        public void SetActiveWebhookEndpoint(string endpointId, string endpointUrl, string signingSecret)
        {
            if (UseSandbox)
            {
                TestWebhookEndpointId = endpointId;
                TestWebhookEndpointUrl = endpointUrl;
                if (!string.IsNullOrWhiteSpace(signingSecret))
                    TestWebhookSecret = signingSecret;
                return;
            }

            WebhookEndpointId = endpointId;
            WebhookEndpointUrl = endpointUrl;
            if (!string.IsNullOrWhiteSpace(signingSecret))
                WebhookSecret = signingSecret;
        }
    }
}
