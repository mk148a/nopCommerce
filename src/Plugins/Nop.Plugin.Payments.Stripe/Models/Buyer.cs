using Nop.Core.Domain.Customers;

namespace Nop.Plugin.Payments.Stripe.Models
{
    public class Buyer
    {
        public Customer Customer { get; set; }
        public string Id { get; set; }
        public Address? shippinAddress { get; set; }
        public Address billingAddress { get; set; }
        public string Ip { get; set; }
        public string RegistrationDate { get; set; }
        public string LastLoginDate { get; set; }
    }
}