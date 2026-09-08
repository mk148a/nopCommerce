
using System.Threading.Tasks;
using Nop.Plugin.Payments.StripeApplePay.Models;

namespace Nop.Plugin.Payments.StripeApplePay.Services
{
    public interface IPaymentStripeApplePayService
    {
        Task<Buyer> GetBuyer(int customerId);
    }
}