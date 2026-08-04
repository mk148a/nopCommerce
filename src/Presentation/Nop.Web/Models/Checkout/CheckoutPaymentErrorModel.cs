using System.Collections.Generic;
using Nop.Web.Framework.Models;

namespace Nop.Web.Models.Checkout;

public record CheckoutPaymentErrorModel : BaseNopModel
{
    public IList<string> Warnings { get; set; } = new List<string>();
}
