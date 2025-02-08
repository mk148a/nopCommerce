using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Stripe.Components
{
    public class Stripe3DSecureViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke(string clientSecret)
        {
            return View("~/Plugins/Payments.Stripe/Views/Stripe3DSecure.cshtml", clientSecret);
        }
    }
} 