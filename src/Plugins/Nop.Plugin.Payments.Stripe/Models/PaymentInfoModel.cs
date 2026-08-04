using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.Stripe.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        public PaymentInfoModel()
        {
        }
        [NopResourceDisplayName("Payment.SelectCreditCard")]
        public string CreditCardType { get; set; }

        [NopResourceDisplayName("Payment.SelectCreditCard")]
        public IList<SelectListItem> CreditCardTypes { get; set; } = new List<SelectListItem>();

        [NopResourceDisplayName("Payment.CardholderName")]
        public string CardholderName { get; set; }

        [NopResourceDisplayName("Payment.CardNumber")]
        public string CardNumber { get; set; }

        [NopResourceDisplayName("Payment.ExpirationDate")]
        public string ExpireMonth { get; set; }

        [NopResourceDisplayName("Payment.ExpirationDate")]
        public string ExpireYear { get; set; }

        public IList<SelectListItem> ExpireMonths { get; set; } = new List<SelectListItem>();

        public IList<SelectListItem> ExpireYears { get; set; } = new List<SelectListItem>();

        [NopResourceDisplayName("Payment.CardCode")]
        public string CardCode { get; set; }

        public string StripeToken { get; set; }

        /// <summary>
        /// Public key used by the plugin's inline 3DS confirmation handler.
        /// It is safe to render; the secret key never leaves the server.
        /// </summary>
        public string StripePublishableKey { get; set; }

    public string Html { get; set; }
        public bool PaymentResult = true;

    }
}
