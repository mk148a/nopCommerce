using System.Text.RegularExpressions;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.GoogleBotAggregator.Domain;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.GoogleBotAggregator.Services;

/// <summary>
/// Google Bot servis implementasyonu
/// </summary>
public class GoogleBotService : IGoogleBotService
{
    #region Fields

    private readonly IRepository<GoogleBotCustomer> _googleBotCustomerRepository;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IProductService _productService;
    private readonly GoogleBotAggregatorSettings _settings;

    #endregion

    #region Ctor

    public GoogleBotService(
        IRepository<GoogleBotCustomer> googleBotCustomerRepository,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        IShoppingCartService shoppingCartService,
        IProductService productService,
        GoogleBotAggregatorSettings settings)
    {
        _googleBotCustomerRepository = googleBotCustomerRepository;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _shoppingCartService = shoppingCartService;
        _productService = productService;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Google Bot müşterisi olup olmadığını kontrol eder
    /// </summary>
    /// <param name="userAgent">User agent</param>
    /// <returns>
    /// True ise Google Bot müşterisidir
    /// </returns>
    public virtual async Task<bool> IsGoogleBotAsync(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return false;

        // Google Bot user agent pattern'leri
        var patterns = new[]
        {
            @"Googlebot",
            @"AdsBot-Google",
            @"Mediapartners-Google",
            @"APIs-Google",
            @"Google-Read-Aloud",
            @"Google-Site-Verification",
            @"Chrome-Lighthouse"
        };

        return patterns.Any(pattern => Regex.IsMatch(userAgent, pattern, RegexOptions.IgnoreCase));
    }

    /// <summary>
    /// Google Bot müşterisini kaydeder
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="userAgent">User agent</param>
    /// <param name="ipAddress">IP address</param>
    /// <returns>Google Bot customer</returns>
    public virtual async Task<GoogleBotCustomer> InsertGoogleBotCustomerAsync(int customerId, string userAgent, string ipAddress)
    {
        var googleBotCustomer = new GoogleBotCustomer
        {
            CustomerId = customerId,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            CreatedOnUtc = DateTime.UtcNow
        };

        await _googleBotCustomerRepository.InsertAsync(googleBotCustomer);

        return googleBotCustomer;
    }

    /// <summary>
    /// Google Bot müşterisini getirir
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>Google Bot customer</returns>
    public virtual async Task<GoogleBotCustomer> GetGoogleBotCustomerByCustomerIdAsync(int customerId)
    {
        if (customerId == 0)
            return null;

        return await Task.FromResult(_googleBotCustomerRepository.Table
            .FirstOrDefault(x => x.CustomerId == customerId));
    }

    /// <summary>
    /// Google Bot müşteri hesabını oluşturur veya getirir
    /// </summary>
    /// <returns>Customer</returns>
    public virtual async Task<Customer> GetOrCreateGoogleBotCustomerAsync()
    {
        var customer = await _customerService.GetCustomerByEmailAsync(_settings.GoogleBotCustomerEmail);
        if (customer != null)
            return customer;

        customer = new Customer
        {
            CustomerGuid = Guid.NewGuid(),
            Email = _settings.GoogleBotCustomerEmail,
            Username = "GoogleBot",
            Active = true,
            CreatedOnUtc = DateTime.UtcNow,
            LastActivityDateUtc = DateTime.UtcNow,
        };

        await _customerService.InsertCustomerAsync(customer);

        // Google Bot olarak işaretle
        await _genericAttributeService.SaveAttributeAsync(customer, "IsGoogleBot", true);

        return customer;
    }

    /// <summary>
    /// Sepetleri birleştirir
    /// </summary>
    /// <param name="sourceCustomerId">Kaynak müşteri ID</param>
    /// <param name="targetCustomerId">Hedef müşteri ID</param>
    public virtual async Task MergeShoppingCartsAsync(int sourceCustomerId, int targetCustomerId)
    {
        if (!_settings.MergeShoppingCarts)
            return;

        var sourceCustomer = await _customerService.GetCustomerByIdAsync(sourceCustomerId);
        var targetCustomer = await _customerService.GetCustomerByIdAsync(targetCustomerId);

        if (sourceCustomer == null || targetCustomer == null)
            return;

        // Kaynak müşterinin sepetini al
        var sourceShoppingCart = await _shoppingCartService.GetShoppingCartAsync(sourceCustomer, ShoppingCartType.ShoppingCart);
        
        // Her bir ürünü hedef müşterinin sepetine ekle
        foreach (var item in sourceShoppingCart)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            if (product != null)
            {
                await _shoppingCartService.AddToCartAsync(targetCustomer, product,
                    ShoppingCartType.ShoppingCart, item.StoreId, item.AttributesXml,
                    item.CustomerEnteredPrice, item.RentalStartDateUtc, item.RentalEndDateUtc,
                    item.Quantity);
            }
        }

        // Kaynak müşterinin sepetini temizle
        foreach (var item in sourceShoppingCart)
        {
            await _shoppingCartService.DeleteShoppingCartItemAsync(item);
        }
    }

    #endregion
} 