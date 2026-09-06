using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using NopStation.Plugin.Misc.Core.Controllers;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Controllers;

[AutoValidateAntiforgeryToken]
public class GtmEventSendController : NopStationPublicController
{
	private readonly IStoreContext _storeContext;

	private readonly ISettingService _settingService;

	private readonly IProductService _productService;

	private readonly ICategoryService _categoryService;

	private readonly ILogger _logger;

	private readonly IWorkContext _workContext;

	private readonly IManufacturerService _manufacturerService;

	private readonly IShoppingCartService _shoppingCartService;

	private readonly IPriceCalculationService _priceCalculationService;

	private readonly OrderSettings _orderSettings;

	private readonly ICurrencyService _currencyService;

	private readonly IProductAttributeService _productAttributeService;

	private readonly IShippingPluginManager _shippingPluginManager;

	private readonly IPaymentPluginManager _paymentPluginManager;

	private readonly ITaxService _taxService;

	public GtmEventSendController(IStoreContext storeContext, ISettingService settingService, IProductService productService, ICategoryService categoryService, ILogger logger, IWorkContext workContext, IManufacturerService manufacturerService, IShoppingCartService shoppingCartService, IPriceCalculationService priceCalculationService, OrderSettings orderSettings, ICurrencyService currencyService, IProductAttributeService productAttributeService, IShippingPluginManager shippingPluginManager, IPaymentPluginManager paymentPluginManager, ITaxService taxService)
	{
		_storeContext = storeContext;
		_settingService = settingService;
		_productService = productService;
		_categoryService = categoryService;
		_logger = logger;
		_workContext = workContext;
		_manufacturerService = manufacturerService;
		_shoppingCartService = shoppingCartService;
		_priceCalculationService = priceCalculationService;
		_orderSettings = orderSettings;
		_currencyService = currencyService;
		_productAttributeService = productAttributeService;
		_shippingPluginManager = shippingPluginManager;
		_paymentPluginManager = paymentPluginManager;
		_taxService = taxService;
	}

	private string FixIllegalJavaScriptChars(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		text = text.Replace("'", "\\'");
		return text;
	}

	private async Task<List<string>> GetCategoriesAsync(int productId)
	{
		IList<ProductCategory> list = await _categoryService.GetProductCategoriesByProductIdAsync(productId, false);
		List<string> categories = new List<string>();
		foreach (ProductCategory item in list)
		{
			categories.Add(FixIllegalJavaScriptChars((await _categoryService.GetCategoryByIdAsync(item.CategoryId)).Name));
		}
		return categories;
	}

	private async Task<(List<object>, List<string>, decimal)> GetProducts()
	{
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		Store store = await _storeContext.GetCurrentStoreAsync();
		string currencyCode = (await _workContext.GetWorkingCurrencyAsync()).CurrencyCode;
		IList<ShoppingCartItem> cart = await _shoppingCartService.GetShoppingCartAsync(customer, (ShoppingCartType?)(ShoppingCartType)1, ((BaseEntity)store).Id, (int?)null, (DateTime?)null, (DateTime?)null);
		decimal total = default(decimal);
		List<object> products = new List<object>();
		List<string> productIds = new List<string>();
		for (int i = 0; i < cart.Count; i++)
		{
			ShoppingCartItem product = cart[i];
			Product productModel = await _productService.GetProductByIdAsync(product.ProductId);
			string manufacturer = "";
			ProductManufacturer val = (await _manufacturerService.GetProductManufacturersByProductIdAsync(((BaseEntity)product).Id, true)).FirstOrDefault();
			if (val != null)
			{
				Manufacturer val2 = await _manufacturerService.GetManufacturerByIdAsync(val.ManufacturerId);
				manufacturer = ((val2 != null) ? val2.Name : null);
			}
			(decimal, decimal, decimal, List<Discount>) tuple = await _priceCalculationService.GetFinalPriceAsync(productModel, customer, store, 0m, true, 1);
			decimal finalPriceWithoutDiscounts = tuple.Item1;
			decimal finalPrice = tuple.Item2;
			decimal item = tuple.Item3;
			List<Discount> item2 = tuple.Item4;
			string sku = FixIllegalJavaScriptChars(string.IsNullOrEmpty(productModel.Sku) ? ((BaseEntity)productModel).Id.ToString() : productModel.Sku);
			string name = FixIllegalJavaScriptChars(productModel.Name);
			string coupon = FixIllegalJavaScriptChars((item2.Count > 0) ? item2[0].Name : "");
			string currencyCode2 = FixIllegalJavaScriptChars(currencyCode);
			decimal discount = item;
			double index = i;
			string brand = FixIllegalJavaScriptChars(manufacturer);
			var anon = new
			{
				Sku = sku,
				Name = name,
				Affiliation = "",
				Coupon = coupon,
				CurrencyCode = currencyCode2,
				Discount = discount,
				Index = index,
				Brand = brand,
				Categories = await GetCategoriesAsync(product.ProductId),
				ItemListId = "related_products",
				ItemListName = "Related Products",
				Price = finalPriceWithoutDiscounts,
				Quantity = product.Quantity,
				Copy = product.Quantity
			};
			total += finalPrice;
			productIds.Add(anon.Sku);
			products.Add(anon);
		}
		return (products, productIds, total);
	}

	private async Task<bool> IsPluginActive()
	{
		Store val = await _storeContext.GetCurrentStoreAsync();
		return (await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(((BaseEntity)val).Id)).IsEnable;
	}

	private async Task<(object, string)> GetProductObjectAsync(int productId, int index)
	{
		Store store = await _storeContext.GetCurrentStoreAsync();
		Product product = await _productService.GetProductByIdAsync(productId);
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		string currencyCode = (await _workContext.GetWorkingCurrencyAsync()).CurrencyCode;
		string manufacturer = "";
		ProductManufacturer val = (await _manufacturerService.GetProductManufacturersByProductIdAsync(productId, true)).FirstOrDefault();
		if (val != null)
		{
			Manufacturer val2 = await _manufacturerService.GetManufacturerByIdAsync(val.ManufacturerId);
			manufacturer = ((val2 != null) ? val2.Name : null);
		}
		(decimal, decimal, decimal, List<Discount>) tuple = await _priceCalculationService.GetFinalPriceAsync(product, customer, store, 0m, true, 1);
		decimal finalPriceWithoutDiscounts = tuple.Item1;
		decimal item = tuple.Item3;
		List<Discount> item2 = tuple.Item4;
		string sku = FixIllegalJavaScriptChars(string.IsNullOrEmpty(product.Sku) ? ((BaseEntity)product).Id.ToString() : product.Sku);
		string name = FixIllegalJavaScriptChars(product.Name);
		string coupon = FixIllegalJavaScriptChars((item2.Count > 0) ? item2[0].Name : "");
		string currencyCode2 = FixIllegalJavaScriptChars(currencyCode);
		decimal discount = item;
		double index2 = index;
		string brand = FixIllegalJavaScriptChars(manufacturer);
		var anon = new
		{
			Sku = sku,
			Name = name,
			Affiliation = "",
			Coupon = coupon,
			CurrencyCode = currencyCode2,
			Discount = discount,
			Index = index2,
			Brand = brand,
			Categories = await GetCategoriesAsync(((BaseEntity)product).Id),
			ItemListId = "related_products",
			ItemListName = "Related Products",
			Price = finalPriceWithoutDiscounts,
			Quantity = 1
		};
		return (anon, anon.Sku);
	}

	[HttpGet]
	public async Task<IActionResult> ProductDetails(int productId, bool isClickedFromProductDetailsPage, int quantity, bool isShoppingCart = true)
	{
		IActionResult result = default(IActionResult);
		object obj2;
		int num;
		try
		{
			Customer customer = await _workContext.GetCurrentCustomerAsync();
			Store store = await _storeContext.GetCurrentStoreAsync();
			await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(((BaseEntity)store).Id);
			if (!(await IsPluginActive()))
			{
				result = ((Controller)(object)this).Json((object?)new
				{
					Result = false,
					Message = "Plugin Disabled!"
				});
				return result;
			}
			await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(productId);
			new CultureInfo("en-US");
			Product product = await _productService.GetProductByIdAsync(productId);
			int[] allowedQuantities = _productService.ParseAllowedQuantities(product);
			IList<ProductAttributeMapping> source = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(((BaseEntity)product).Id);
			bool flag = false;
			if ((int)product.ProductType != 5 || product.OrderMinimumQuantity > 1 || product.CustomerEntersPrice || product.IsRental || allowedQuantities.Length != 0 || source.Any((ProductAttributeMapping pam) => (int)pam.AttributeControlType != 50))
			{
				flag = true;
			}
			if (!isClickedFromProductDetailsPage && flag)
			{
				result = ((Controller)(object)this).Json((object?)new
				{
					Result = false,
					Message = "Something wrong!"
				});
				return result;
			}
			string manufacturer = "";
			ProductManufacturer val = (await _manufacturerService.GetProductManufacturersByProductIdAsync(((BaseEntity)product).Id, true)).FirstOrDefault();
			if (val != null)
			{
				Manufacturer val2 = await _manufacturerService.GetManufacturerByIdAsync(val.ManufacturerId);
				manufacturer = ((val2 != null) ? val2.Name : null);
			}
			ShoppingCartType value = (ShoppingCartType)1;
			if (!isShoppingCart)
			{
				value = (ShoppingCartType)2;
			}
			ShoppingCartItem latestItem = (await _shoppingCartService.GetShoppingCartAsync(customer, (ShoppingCartType?)value, ((BaseEntity)store).Id, (int?)null, (DateTime?)null, (DateTime?)null)).OrderByDescending((ShoppingCartItem val4) => (!(val4.CreatedOnUtc > val4.UpdatedOnUtc)) ? val4.UpdatedOnUtc : val4.CreatedOnUtc).FirstOrDefault();
			if (quantity == 0)
			{
				quantity = product.OrderMinimumQuantity;
			}
			int copy = quantity;
			Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
			decimal unitPriceValue;
			if (product.CallForPrice && (!_orderSettings.AllowAdminsToBuyCallForPriceProducts || _workContext.OriginalCustomerIfImpersonated == null))
			{
				unitPriceValue = default(decimal);
			}
			else
			{
				ITaxService taxService = _taxService;
				Product val3 = product;
				(decimal, decimal) obj = await taxService.GetProductPriceAsync(val3, (await _shoppingCartService.GetUnitPriceAsync(latestItem, true)).Item1);
				decimal item = obj.Item1;
				unitPriceValue = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(item, currentCurrency);
			}
			decimal price = unitPriceValue;
			(decimal, decimal, decimal, List<Discount>) tuple = await _priceCalculationService.GetFinalPriceAsync(product, customer, store, 0m, true, product.OrderMinimumQuantity);
			decimal discountAmount = tuple.Item3;
			List<Discount> appliedDiscounts = tuple.Item4;
			discountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(discountAmount, currentCurrency);
			string name = FixIllegalJavaScriptChars(product.Name);
			string sku = FixIllegalJavaScriptChars(string.IsNullOrEmpty(product.Sku) ? ((BaseEntity)product).Id.ToString() : product.Sku);
			string coupon = FixIllegalJavaScriptChars((appliedDiscounts.Count > 0) ? appliedDiscounts[0].Name : "");
			decimal discount = discountAmount;
			string manufacturer2 = FixIllegalJavaScriptChars(manufacturer);
			var data = new
			{
				Name = name,
				Sku = sku,
				Affiliation = "",
				Coupon = coupon,
				Discount = discount,
				Index = 0.0,
				Manufacturer = manufacturer2,
				Categories = await GetCategoriesAsync(productId),
				Price = price,
				Quaantity = copy,
				Currency = currentCurrency.CurrencyCode,
				Copy = copy
			};
			result = ((Controller)(object)this).Json((object?)new
			{
				Result = true,
				Data = data,
				Price = (unitPriceValue * (decimal)copy - discountAmount).ToString("0.00")
			});
			return result;
		}
		catch (Exception ex)
		{
			obj2 = ex;
			num = 1;
		}
		if (num != 1)
		{
			return result;
		}
		Exception ex2 = (Exception)obj2;
		await _logger.InsertLogAsync((LogLevel)40, "Error in GTM tracking: ", ex2.ToString(), (Customer)null);
		return ((Controller)(object)this).Json((object?)new
		{
			Result = false,
			Message = "Something wrong!"
		});
	}

	[HttpPost]
	public async Task<IActionResult> ShoppingCartDetails(string systemName = "")
	{
		if (!(await IsPluginActive()))
		{
			return ((Controller)(object)this).Json((object?)new
			{
				Result = false,
				Message = "Plugin Disabled!"
			});
		}
		string currencyCode = (await _workContext.GetWorkingCurrencyAsync()).CurrencyCode;
		(List<object>, List<string>, decimal) tuple = await GetProducts();
		List<object> products = tuple.Item1;
		List<string> productIds = tuple.Item2;
		decimal total = tuple.Item3;
		IShippingRateComputationMethod shippingPlugin = await ((IPluginManager<IShippingRateComputationMethod>)(object)_shippingPluginManager).LoadPluginBySystemNameAsync(systemName, (Customer)null, 0);
		IPaymentMethod val = await ((IPluginManager<IPaymentMethod>)(object)_paymentPluginManager).LoadPluginBySystemNameAsync(systemName, (Customer)null, 0);
		string name = string.Empty;
		if (shippingPlugin != null)
		{
			name = ((IPlugin)shippingPlugin).PluginDescriptor.FriendlyName;
		}
		if (val != null)
		{
			name = ((IPlugin)val).PluginDescriptor.FriendlyName;
		}
		return ((Controller)(object)this).Json((object?)new
		{
			Result = true,
			Products = products,
			ProductIds = productIds,
			Currency = currencyCode,
			Total = total,
			Name = name
		});
	}

	[HttpPost]
	public async Task<IActionResult> GetProducts(IList<int> productIds)
	{
		if (!(await IsPluginActive()))
		{
			return ((Controller)(object)this).Json((object?)new
			{
				Result = false,
				Message = "Plugin Disabled!"
			});
		}
		List<object> products = new List<object>();
		List<string> productIDs = new List<string>();
		for (int i = 0; i < productIds.Count; i++)
		{
			var (item, item2) = await GetProductObjectAsync(productIds[i], i);
			productIDs.Add(item2);
			products.Add(item);
		}
		return ((Controller)(object)this).Json((object?)new
		{
			Result = true,
			ProductIds = productIDs,
			Products = products
		});
	}
}
