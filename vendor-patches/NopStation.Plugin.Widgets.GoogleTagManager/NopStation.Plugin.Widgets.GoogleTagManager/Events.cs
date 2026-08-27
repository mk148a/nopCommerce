using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Events;
using Nop.Web.Factories;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.UI;
using Nop.Web.Framework.UI.Paging;
using Nop.Web.Models.Catalog;
using NopStation.Plugin.Widgets.GoogleTagManager.Services;

namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class Events : IConsumer<EntityDeletedEvent<ShoppingCartItem>>, IConsumer<PageRenderingEvent>, IConsumer<CustomerRegisteredEvent>, IConsumer<ProductSearchEvent>
{
	private readonly INopHtmlHelper _nopHtmlHelper;

	private readonly IGTMService _gtm_Service;

	private readonly IPriceCalculationService _priceCalculationService;

	private readonly IHttpContextAccessor _httpContextAccessor;

	private readonly IWorkContext _workContext;

	private readonly ICategoryService _categoryService;

	private readonly ICatalogModelFactory _catalogModelFactory;

	private readonly IStoreContext _storeContext;

	private readonly ISettingService _settingService;

	private readonly ICustomerService _customerService;

	private readonly IProductService _productService;

	private readonly ICurrencyService _currencyService;

	public Events(INopHtmlHelper nopHtmlHelper, IGTMService gtm_Service, IPriceCalculationService priceCalculationService, IHttpContextAccessor httpContextAccessor, IWorkContext workContext, ICategoryService categoryService, ICatalogModelFactory catalogModelFactory, IStoreContext storeContext, ISettingService settingService, ICustomerService customerService, IProductService productService, ICurrencyService currencyService)
	{
		_nopHtmlHelper = nopHtmlHelper;
		_gtm_Service = gtm_Service;
		_priceCalculationService = priceCalculationService;
		_httpContextAccessor = httpContextAccessor;
		_workContext = workContext;
		_categoryService = categoryService;
		_catalogModelFactory = catalogModelFactory;
		_storeContext = storeContext;
		_settingService = settingService;
		_customerService = customerService;
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

	private async Task<bool> IsPluginActive()
	{
		Store val = await _storeContext.GetCurrentStoreAsync();
		return (await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(((BaseEntity)val).Id)).IsEnable;
	}

	public async Task HandleEventAsync(EntityDeletedEvent<ShoppingCartItem> eventMessage)
	{
		if (!(await IsPluginActive()))
		{
			return;
		}
		Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
		Store store = await _storeContext.GetCurrentStoreAsync();
		RouteData routeData = _httpContextAccessor.HttpContext.GetRouteData();
		string text = routeData.Values["controller"]?.ToString();
		string text2 = routeData.Values["action"]?.ToString();
		if (!text.Equals("Checkout", StringComparison.InvariantCultureIgnoreCase) || (!text2.Equals("OpcConfirmOrder", StringComparison.InvariantCultureIgnoreCase) && !text2.Equals("Confirm", StringComparison.InvariantCultureIgnoreCase)))
		{
			ShoppingCartItem item = eventMessage.Entity;
			if ((int)item.ShoppingCartType != 2)
			{
				Product product = await _productService.GetProductByIdAsync(item.ProductId);
				string script = "\r\n                <script>\r\n                    window.dataLayer = window.dataLayer || [];\r\n                    dataLayer.push({\r\n                         'ecommerce':undefined\r\n                    });\r\n                    dataLayer.push({\r\n                        'event': '%event_name%', \r\n                        'var_prodid': ['%product_id%'],\r\n                        'var_pagetype' : '%page_type%',\r\n                        'currency': '%currency%',\r\n                        'var_prodval':%value%,\r\n                        'ecommerce':%ecommerce%\r\n                    });\r\n                </script>";
				string ecommerceScript = await _gtm_Service.PrepareRemoveFromCartEcommerceAsync(item);
				Customer val = await _workContext.GetCurrentCustomerAsync();
				string sku = (string.IsNullOrEmpty(product.Sku) ? ((BaseEntity)product).Id.ToString() : product.Sku);
				(decimal, decimal, decimal, List<Discount>) obj = await _priceCalculationService.GetFinalPriceAsync(product, val, store, 0m, true, item.Quantity);
				decimal item2 = obj.Item1;
				decimal item3 = obj.Item3;
				decimal num = item2 * (decimal)item.Quantity - item3;
				num = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(num, currentCurrency);
				script = script.Replace("%ecommerce%", ecommerceScript);
				script = script.Replace("%event_name%", GoogleTagManagerDefaults.REMOVE_TO_CART);
				script = script.Replace("%product_id%", sku);
				script = script.Replace("%currency%", currentCurrency.CurrencyCode);
				script = script.Replace("%value%", num.ToString("0.00", CultureInfo.InvariantCulture));
				script = script.Replace("%page_type%", GoogleTagManagerDefaults.CART_PAGE);
				_nopHtmlHelper.AddInlineScriptParts((ResourceLocation)3, script);
			}
		}
	}

	public async Task HandleEventAsync(PageRenderingEvent eventMessage)
	{
		if (!(await IsPluginActive()))
		{
			return;
		}
		HttpContext? httpContext = _httpContextAccessor.HttpContext;
		RouteData routeData = httpContext.GetRouteData();
		string text = routeData.Values["controller"]?.ToString();
		string text2 = routeData.Values["action"]?.ToString();
		HttpRequest request = httpContext.Request;
		string text3 = request.Query["viewmode"].ToString();
		StringValues stringValues = request.Query["orderby"];
		StringValues stringValues2 = request.Query["pagesize"];
		StringValues stringValues3 = request.Query["pagenumber"];
		if (text.Equals("catalog", StringComparison.InvariantCultureIgnoreCase) && text2.Equals("category", StringComparison.InvariantCultureIgnoreCase))
		{
			int num = int.Parse(routeData.Values["categoryId"]?.ToString());
			CatalogProductsCommand command = new CatalogProductsCommand();
			if (!string.IsNullOrEmpty(text3))
			{
				command.ViewMode = text3;
			}
			if (int.TryParse(stringValues, out var result))
			{
				command.OrderBy = result;
			}
			if (int.TryParse(stringValues2, out var result2))
			{
				((BasePageableModel)command).PageSize = result2;
			}
			if (int.TryParse(stringValues3, out var result3))
			{
				((BasePageableModel)command).PageNumber = result3;
			}
			Category val = await _categoryService.GetCategoryByIdAsync(num);
			IList<ProductOverviewModel> products = (await _catalogModelFactory.PrepareCategoryModelAsync(val, command)).CatalogProductsModel.Products;
			string currencyCode = (await _workContext.GetWorkingCurrencyAsync()).CurrencyCode;
			if (products.Count == 0)
			{
				return;
			}
			string categoryScript = "\r\n                <script>\r\n                    window.dataLayer = window.dataLayer || [];\r\n                    dataLayer.push({\r\n                        'event': '%event_name%', \r\n                        'var_prodid': [%product_ids%],\r\n                        'var_pagetype' : '%page_type%',\r\n                        'currency': '%currency%',\r\n                        'ecommerce':%ecommerce%\r\n                    });\r\n                </script>\r\n                ";
			StringBuilder stringBuilder = new StringBuilder();
			foreach (ProductOverviewModel item in products)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(",");
				}
				string text4 = item.Sku;
				if (text4 == null || string.IsNullOrEmpty(text4))
				{
					text4 = ((BaseNopEntityModel)item).Id.ToString();
				}
				stringBuilder.AppendLine("'" + FixIllegalJavaScriptChars(text4) + "'");
			}
			categoryScript = categoryScript.Replace("%event_name%", GoogleTagManagerDefaults.VIEW_ITEM_LIST);
			categoryScript = categoryScript.Replace("%page_type%", GoogleTagManagerDefaults.CATEGORY_VIEW);
			categoryScript = categoryScript.Replace("%product_ids%", stringBuilder.ToString());
			categoryScript = categoryScript.Replace("%ecommerce%", await _gtm_Service.GetCategoryEcommerceScript(products));
			categoryScript = categoryScript.Replace("%currency%", currencyCode);
			_nopHtmlHelper.AddInlineScriptParts((ResourceLocation)3, categoryScript);
		}
		else if (text.Equals("Common", StringComparison.InvariantCultureIgnoreCase) && text2.Equals("ContactUs", StringComparison.InvariantCultureIgnoreCase))
		{
			string text5 = "\r\n                <script>\r\n                    window.dataLayer = window.dataLayer || [];\r\n                    dataLayer.push({\r\n                        'event': '%event_name%', \r\n                    });\r\n                </script>\r\n                ";
			text5 = text5.Replace("%event_name%", GoogleTagManagerDefaults.CONTACT_US);
			_nopHtmlHelper.AddInlineScriptParts((ResourceLocation)3, text5);
		}
		else if (text.Equals("Home", StringComparison.InvariantCultureIgnoreCase) && text2.Equals("Index", StringComparison.InvariantCultureIgnoreCase))
		{
			string text6 = "\r\n                <script>\r\n                    window.dataLayer = window.dataLayer || [];\r\n                    dataLayer.push({\r\n                        'event': '%event_name%', \r\n                    });\r\n                </script>";
			text6 = text6.Replace("%event_name%", GoogleTagManagerDefaults.HOME_PAGE);
			_nopHtmlHelper.AddInlineScriptParts((ResourceLocation)3, text6);
		}
	}

	public async Task HandleEventAsync(CustomerRegisteredEvent eventMessage)
	{
		if (await IsPluginActive())
		{
			string script = "\r\n                        'event': '%event_name%', \r\n                        'user_name': '%user_name%',\r\n                        'user_email' : '%user_email%'";
			Customer user = ((eventMessage != null) ? eventMessage.Customer : null);
			if (user != null)
			{
				string text = await _customerService.GetCustomerFullNameAsync(user);
				script = script.Replace("%event_name%", GoogleTagManagerDefaults.CUSTOMER_REGISTER);
				script = script.Replace("%user_name%", FixIllegalJavaScriptChars(text));
				script = script.Replace("%user_email%", FixIllegalJavaScriptChars(user.Email));
				string key = string.Format(GoogleTagManagerDefaults.SessionKey, user.CustomerGuid.ToString());
				_httpContextAccessor.HttpContext.Session.SetString(key, script);
			}
		}
	}

	public async Task HandleEventAsync(ProductSearchEvent eventMessage)
	{
		if (await IsPluginActive())
		{
			string searchTerm = eventMessage.SearchTerm;
			string text = "\r\n                <script>\r\n                    window.dataLayer = window.dataLayer || [];\r\n                    dataLayer.push({\r\n                        'event': '%event_name%', \r\n                         'searchKey':'%search_key%'\r\n                    });\r\n                </script>";
			text = text.Replace("%event_name%", GoogleTagManagerDefaults.SEARCH);
			text = text.Replace("%search_key%", FixIllegalJavaScriptChars(searchTerm));
			_nopHtmlHelper.AddInlineScriptParts((ResourceLocation)3, text);
		}
	}
}
