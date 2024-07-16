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

        public string StripePublishableKey { get; set; }
        public string Currency { get; set; }
        public string Country { get; set; }

        public string PaymentMethodId { get; set; }


    }
}