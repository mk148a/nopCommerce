using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.GoogleBotAggregator.Domain;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Services;

/// <summary>
/// Google Bot servis interface'i
/// </summary>
public interface IGoogleBotService
{
    /// <summary>
    /// Google Bot müşterisi olup olmadığını kontrol eder
    /// </summary>
    /// <param name="userAgent">User agent</param>
    /// <returns>
    /// True ise Google Bot müşterisidir
    /// </returns>
    Task<bool> IsGoogleBotAsync(string userAgent);

    /// <summary>
    /// Google Bot müşterisini kaydeder
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="userAgent">User agent</param>
    /// <param name="ipAddress">IP address</param>
    /// <returns>Google Bot customer</returns>
    Task<GoogleBotCustomer> InsertGoogleBotCustomerAsync(int customerId, string userAgent, string ipAddress);

    /// <summary>
    /// Google Bot müşterisini getirir
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>Google Bot customer</returns>
    Task<GoogleBotCustomer> GetGoogleBotCustomerByCustomerIdAsync(int customerId);

    /// <summary>
    /// Google Bot müşteri hesabını oluşturur veya getirir
    /// </summary>
    /// <returns>Customer</returns>
    Task<Customer> GetOrCreateGoogleBotCustomerAsync();

    /// <summary>
    /// Sepetleri birleştirir
    /// </summary>
    /// <param name="sourceCustomerId">Kaynak müşteri ID</param>
    /// <param name="targetCustomerId">Hedef müşteri ID</param>
    Task MergeShoppingCartsAsync(int sourceCustomerId, int targetCustomerId);
} 