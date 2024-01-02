
using System.Threading.Tasks;
using Nop.Plugin.Payments.Stripe.Models;

namespace Nop.Plugin.Payments.Stripe.Services
{
    public interface IPaymentStripeService
    {
        Task<Buyer> GetBuyer(int customerId);
    }
}