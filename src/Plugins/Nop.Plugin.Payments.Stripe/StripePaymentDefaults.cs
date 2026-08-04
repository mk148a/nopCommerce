using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Stripe
{
    public static class StripePaymentDefaults
    {
        /// <summary>
        /// Stripe payment method system name
        /// </summary>
        public static string SystemName => "Payments.Stripe";

        /// <summary>
        /// Name of the view component to display plugin in public store
        /// </summary>
        public const string ViewComponentName = "PaymentStripe";

        /// <summary>
        /// User agent used for requesting Stripe services
        /// </summary>
        public static string UserAgent => "Stripe-connect-nopCommerce-4.5-1.0";

        /// <summary>
        /// Path to the Stripe payment form js script
        /// </summary>
        public static string PaymentFormScriptPath => "https://js.stripe.com/v3/";

        /// <summary>
        /// Public endpoint that receives signed Stripe events.
        /// </summary>
        public const string WebhookPath = "PaymentStripe/WebhookHandler";

        /// <summary>
        /// Events required by the nopCommerce PaymentIntent flow.
        /// </summary>
        public static IReadOnlyList<string> WebhookEvents { get; } = new[]
        {
            "payment_intent.succeeded",
            "payment_intent.payment_failed",
            "payment_intent.canceled",
            "payment_intent.processing",
            "payment_intent.requires_action"
        };


        /// <summary>
        /// Note passed for each payment transaction
        /// </summary>
        /// <remarks>
        /// {0} : Order Guid
        /// </remarks>
        public static string PaymentNote => "nopCommerce Order Id: {0}";
    }
}
