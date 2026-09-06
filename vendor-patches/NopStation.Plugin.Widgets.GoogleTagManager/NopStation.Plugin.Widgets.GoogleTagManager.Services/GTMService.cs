using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Web.Factories;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.ShoppingCart;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Services;

public class GTMService : IGTMService
{
	private readonly IManufacturerService _manufacturerService;

	private readonly ICategoryService _categoryService;

	private readonly IShoppingCartModelFactory _shoppingCartModelFactory;

	private readonly IOrderService _orderService;

	private readonly IWorkContext _workContext;

	private readonly ICustomerService _customerService;

	private readonly IStoreContext _storeContext;

	private readonly IShoppingCartService _shoppingCartService;

	private readonly IOrderTotalCalculationService _orderTotalCalculationService;

	private readonly IPriceCalculationService _priceCalculationService;

	private readonly IGiftCardService _giftCardService;

	private readonly IProductService _productService;

	private readonly ICurrencyService _currencyService;

	public GTMService(IManufacturerService manufacturerService, ICategoryService categoryService, IShoppingCartModelFactory shoppingCartModelFactory, IOrderService orderService, IWorkContext workContext, ICustomerService customerService, IStoreContext storeContext, IShoppingCartService shoppingCartService, IOrderTotalCalculationService orderTotalCalculationService, IPriceCalculationService priceCalculationService, IGiftCardService giftCardService, IProductService productService, ICurrencyService currencyService)
	{
		_manufacturerService = manufacturerService;
		_categoryService = categoryService;
		_shoppingCartModelFactory = shoppingCartModelFactory;
		_orderService = orderService;
		_workContext = workContext;
		_customerService = customerService;
		_storeContext = storeContext;
		_shoppingCartService = shoppingCartService;
		_orderTotalCalculationService = orderTotalCalculationService;
		_priceCalculationService = priceCalculationService;
		_giftCardService = giftCardService;
		_productService = productService;
		_currencyService = currencyService;
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

	public async Task<string> GetCategoriesAsync(int productId)
	{
		IList<ProductCategory> categories = await _categoryService.GetProductCategoriesByProductIdAsync(productId, false);
		StringBuilder script = new StringBuilder();
		for (int i = 0; i < categories.Count; i++)
		{
			string name = (await _categoryService.GetCategoryByIdAsync(categories[i].CategoryId)).Name;
			string text = "item_category" + ((i > 0) ? (i + 1).ToString() : "");
			string value = "'" + text + "': '" + FixIllegalJavaScriptChars(name) + "',";
			script.AppendLine(value);
		}
		return script.ToString();
	}

	public async Task<string> GetProductDetailsAsync(int productId, int quantity, int index = 0)
	{
		Store store = await _storeContext.GetCurrentStoreAsync();
		Customer customer = await _workContext.GetCurrentCustomerAsync();
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		string itemInformationScript = "{\r\n                                        'item_id': '%item_id%',\r\n                                        'item_name': '%productName%',\r\n                                        'affiliation': '',\r\n                                        'coupon':'%coupon_code%',\r\n                                        'discount':%discount_amount%,\r\n                                        'index':%index_number%,\r\n                                        'item_brand': '%manufacturer%',  \r\n                                         %categories%\r\n                                        'price': %unitPrice%,\r\n                                        'quantity': %quantity%,\r\n                                        'copy': %quantity%,\r\n                                    }";
		string manufacturer = "";
		ProductManufacturer val = (await _manufacturerService.GetProductManufacturersByProductIdAsync(productId, true)).FirstOrDefault();
		if (val != null)
		{
			Manufacturer val2 = await _manufacturerService.GetManufacturerByIdAsync(val.ManufacturerId);
			manufacturer = ((val2 != null) ? val2.Name : null);
		}
		Product product = await _productService.GetProductByIdAsync(productId);
		string sku = product.Sku;
		if (string.IsNullOrEmpty(sku))
		{
			sku = productId.ToString();
		}
		(decimal, decimal, decimal, List<Discount>) tuple = await _priceCalculationService.GetFinalPriceAsync(product, customer, store, 0m, true, quantity);
		decimal discountAmount = tuple.Item3;
		List<Discount> appliedDiscounts = tuple.Item4;
		discountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(discountAmount, currentCurrency);
		string newValue = await GetCategoriesAsync(productId);
		itemInformationScript = itemInformationScript.Replace("%productName%", FixIllegalJavaScriptChars(product.Name));
		itemInformationScript = itemInformationScript.Replace("%categories%", newValue);
		itemInformationScript = itemInformationScript.Replace("%manufacturer%", FixIllegalJavaScriptChars(manufacturer));
		itemInformationScript = itemInformationScript.Replace("%item_id%", FixIllegalJavaScriptChars(sku));
		itemInformationScript = itemInformationScript.Replace("%quantity%", FixIllegalJavaScriptChars(quantity.ToString()));
		string text = itemInformationScript;
		GTMService gTMService = this;
		Discount? obj = appliedDiscounts.FirstOrDefault();
		itemInformationScript = text.Replace("%coupon_code%", gTMService.FixIllegalJavaScriptChars(((obj != null) ? obj.Name : null) ?? ""));
		itemInformationScript = itemInformationScript.Replace("%discount_amount%", FixIllegalJavaScriptChars(discountAmount.ToString("0.00", CultureInfo.InvariantCulture)));
		return itemInformationScript.Replace("%index_number%", index.ToString());
	}

	public async Task<string> PrepareProductItemsAsync(IList<ShoppingCartItem> cart)
	{
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		ShoppingCartModel val = new ShoppingCartModel();
		val = await _shoppingCartModelFactory.PrepareShoppingCartModelAsync(val, cart, true, false, false);
		StringBuilder sb = new StringBuilder();
		int index = 0;
		foreach (ShoppingCartModel.ShoppingCartItemModel item in val.Items)
		{
			if (!string.IsNullOrEmpty(sb.ToString()))
			{
				sb.AppendLine(",");
			}
			decimal unitPriceValue = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(item.UnitPriceValue, currentCurrency);
			string value = (await GetProductDetailsAsync(item.ProductId, item.Quantity, index)).Replace("%unitPrice%", unitPriceValue.ToString("0.00", CultureInfo.InvariantCulture));
			sb.AppendLine(value);
			index++;
		}
		return sb.ToString();
	}

	public async Task<string> PrepareOrderItemsAsync(int orderId)
	{
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		StringBuilder sb = new StringBuilder();
		int index = 0;
		foreach (OrderItem item in await _orderService.GetOrderItemsAsync(orderId, (bool?)null, (bool?)null, 0))
		{
			if (!string.IsNullOrEmpty(sb.ToString()))
			{
				sb.AppendLine(",");
			}
			decimal unitPriceValue = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(item.UnitPriceExclTax, currentCurrency);
			string value = (await GetProductDetailsAsync(item.ProductId, item.Quantity, index)).Replace("%unitPrice%", unitPriceValue.ToString("0.00", CultureInfo.InvariantCulture));
			sb.AppendLine(value);
			index++;
		}
		return sb.ToString();
	}

	public async Task<string> PrepareRemoveFromCartEcommerceAsync(ShoppingCartItem cartItem)
	{
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		string currency = currentCurrency.CurrencyCode;
		string removeFromCartScript = "{\r\n                                         'currency':'%currency%',\r\n                                          'value':%value%,\r\n                                           'price': %unitPrice%,\r\n                                            items : [%items%]\r\n                                         }";
		string items = await GetProductDetailsAsync(cartItem.ProductId, cartItem.Quantity);
		Product val = await _productService.GetProductByIdAsync(cartItem.ProductId);
		decimal num = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(val.Price, currentCurrency);
		removeFromCartScript = removeFromCartScript.Replace("%currency%", currency);
		removeFromCartScript = removeFromCartScript.Replace("%items%", items);
		return removeFromCartScript.Replace("%unitPrice%", num.ToString("0.00", CultureInfo.InvariantCulture));
	}

	public async Task<string> GetProductIdAsync(int productId)
	{
		string text = (await _productService.GetProductByIdAsync(productId)).Sku;
		if (string.IsNullOrEmpty(text))
		{
			text = productId.ToString();
		}
		return FixIllegalJavaScriptChars(text);
	}

	public async Task<string> GetPurchaseEcommerceScriptAsync(int orderId)
	{
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		string orderEcommerceScript = "\r\n                                        'transaction_id': '%orderId%',\r\n                                        'affiliation': '',\r\n                                        'value': %total%,\r\n                                        'tax': %tax%,\r\n                                        'discount':%discount%,\r\n                                        'shipping': %shipping_cost%,\r\n                                        'currency': '%currency%',\r\n                                        'coupon': '%coupon_code%',\r\n                                        'items': [%productInformation%]\r\n                                        ";
		string couponCode = "";
		string orderItemScript = await PrepareOrderItemsAsync(orderId);
		Order order = await _orderService.GetOrderByIdAsync(orderId);
		GiftCardUsageHistory usedGiftCard = (await _giftCardService.GetGiftCardUsageHistoryAsync(order)).FirstOrDefault();
		decimal orderDiscount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(order.OrderDiscount, currentCurrency);
		if (usedGiftCard != null)
		{
			couponCode = (await _giftCardService.GetGiftCardByIdAsync(usedGiftCard.GiftCardId)).GiftCardCouponCode;
		}
		orderEcommerceScript = orderEcommerceScript.Replace("%coupon_code%", couponCode);
		orderEcommerceScript = orderEcommerceScript.Replace("%discount%", orderDiscount.ToString("0.00", CultureInfo.InvariantCulture));
		return orderEcommerceScript.Replace("%productInformation%", orderItemScript);
	}

	public async Task<string> GetCustomerScriptAsync(int customerId)
	{
		Customer customer = await _customerService.GetCustomerByIdAsync(customerId);
		string text = await _customerService.GetCustomerFullNameAsync(customer);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("'user_fullName': '" + FixIllegalJavaScriptChars(text) + "'");
		stringBuilder.AppendLine(",");
		stringBuilder.Append("'user_id': '" + FixIllegalJavaScriptChars(customer.CustomerGuid.ToString()) + "'");
		return stringBuilder.ToString();
	}

	public async Task<string> GetCategoryEcommerceScript(IList<ProductOverviewModel> products)
	{
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		string ecommerce = "{\r\n                              items : [%items%]  \r\n                              }";
		StringBuilder sb = new StringBuilder();
		int index = 0;
		foreach (ProductOverviewModel item in products)
		{
			if (!string.IsNullOrEmpty(sb.ToString()))
			{
				sb.AppendLine(",");
			}
			string script = (await GetProductDetailsAsync(((BaseNopEntityModel)item).Id, 1, index)).Replace("%unitPrice%", (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(item.ProductPrice.PriceValue.GetValueOrDefault(), currentCurrency)).ToString("0.00", CultureInfo.InvariantCulture));
			sb.AppendLine(script);
			index++;
		}
		return ecommerce.Replace("%items%", sb.ToString());
	}

	public async Task<string> PrepareShoppingCartScriptAsync(Customer customer, int storeId)
	{
		IList<ShoppingCartItem> cart = await _shoppingCartService.GetShoppingCartAsync(customer, (ShoppingCartType?)(ShoppingCartType)1, storeId, (int?)null, (DateTime?)null, (DateTime?)null);
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		string currencyCode = currentCurrency.CurrencyCode;
		if (cart.Any())
		{
			decimal cartSubTotal = (await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false)).Item3;
			cartSubTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(cartSubTotal, currentCurrency);
			string viewCartScript = GoogleTagManagerDefaults.BaseEventScript;
			StringBuilder ids = new StringBuilder();
			foreach (ShoppingCartItem item in cart)
			{
				if (ids.Length > 0)
				{
					ids.Append(",");
				}
				StringBuilder stringBuilder = ids;
				stringBuilder.AppendLine("'" + await GetProductIdAsync(item.ProductId) + "'");
			}
			viewCartScript = viewCartScript.Replace("%currency%", FixIllegalJavaScriptChars(currencyCode));
			viewCartScript = viewCartScript.Replace("%event_name%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.VIEW_CART));
			viewCartScript = viewCartScript.Replace("%page_type%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.CART_PAGE));
			viewCartScript = viewCartScript.Replace("%value%", cartSubTotal.ToString("0.00", CultureInfo.InvariantCulture));
			viewCartScript = viewCartScript.Replace("%total%", cartSubTotal.ToString("0.00", CultureInfo.InvariantCulture));
			viewCartScript = viewCartScript.Replace("%product_ids%", ids.ToString());
			return viewCartScript.Replace("%productInformation%", await PrepareProductItemsAsync(cart));
		}
		return "";
	}
}
