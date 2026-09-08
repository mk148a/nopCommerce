using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.StripeApplePay.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        public PaymentInfoModel()
        {
        }

        [NopResourceDisplayName("Payment.CardNumber")]
        public string CardNumber { get; set; }


        public string StripeToken { get; set; }

        public decimal OrderTotal { get; set; }

        /// <summary>
        /// The current checkout total in Stripe's smallest currency unit.
        /// Keeping this as an integer prevents locale formatting and rounding
        /// from turning a wallet total into zero or the wrong amount.
        /// </summary>
        public long OrderTotalMinor { get; set; }

        public string StripePublishableKey { get; set; }
        public string Currency { get; set; }
        public string Country { get; set; }

        public string PaymentMethodId { get; set; }


    }
}
