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

        [NopResourceDisplayName("Payment.OrderTotal")]
        public decimal OrderTotal { get; set; }

        [NopResourceDisplayName("Payment.PaymentIntentId")]
        public string PaymentIntentId { get; set; }

        public string StripePublishableKey { get; set; }
        public string Html { get; set; }
        public bool PaymentResult { get; set; } = true;
    }
}