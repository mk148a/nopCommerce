using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Stripe.Models;
public class PaymentStatusResult
{
    public string Status { get; set; }
    public string OrderId { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
