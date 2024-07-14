using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.StripeApplePay
{
    public static class StripeApplePayPaymentDefaults
    {
        /// <summary>
        /// Stripe payment method system name
        /// </summary>
        public static string SystemName => "Payments.StripeApplePay";

        /// <summary>
        /// Name of the view component to display plugin in public store
        /// </summary>
        public const string ViewComponentName = "StripeApplePay";

        /// <summary>
        /// User agent used for requesting Stripe services
        /// </summary>
        public static string UserAgent => "Stripe-connect-nopCommerce-4.5-1.0";

      
    }
}
