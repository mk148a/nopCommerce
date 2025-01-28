using System;
using FluentValidation;
using Nop.Plugin.Payments.StripeApplePay.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.Stripe.Validators
{
    public partial class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
   
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            //this.RuleFor(x => x.PaymentIntentId)
            //    .NotEmpty()
            //    .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.StripeApplePay.Fields.PaymentIntent.NotFound"));

            this.RuleFor(x => x.PaymentMethodId)
                .NotEmpty()
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.StripeApplePay.Fields.PaymentMethod.NotFound"));
        
        }
    }
}