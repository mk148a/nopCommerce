using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Framework.Infrastructure;
using NopStation.Plugin.Misc.Core.Components;
using NopStation.Plugin.Misc.Core.Services;
using NopStation.Plugin.Widgets.GoogleTagManager.Models;
using NopStation.Plugin.Widgets.GoogleTagManager.Services;

namespace NopStation.Plugin.Widgets.GoogleTagManager.Components;

public class GoogleTagManagerViewComponent : NopStationViewComponent
{
	private readonly GoogleTagManagerSettings _googleTagManagerConfigurationSettings;

	private readonly IProductService _productService;

	private readonly IWorkContext _workContext;

	private readonly IStoreContext _storeContext;

	private readonly INopStationContext _nopStationContext;

	private readonly OrderSettings _orderSettings;

	private readonly IOrderTotalCalculationService _orderTotalCalculationService;

	private readonly ISettingService _settingService;

	private readonly IHttpContextAccessor _httpContextAccessor;

	private readonly IShoppingCartService _shoppingCartService;

	private readonly IGTMService _gtmService;

	private readonly IUrlRecordService _urlRecordService;

	private readonly IPriceCalculationService _priceCalculationService;

	private readonly ICurrencyService _currencyService;

	public GoogleTagManagerViewComponent(GoogleTagManagerSettings googleTagManagerConfigurationSettings, IProductService productService, IWorkContext workContext, IStoreContext storeContext, INopStationContext nopStationContext, OrderSettings orderSettings, IOrderTotalCalculationService orderTotalCalculationService, ISettingService settingService, IHttpContextAccessor httpContextAccessor, IShoppingCartService shoppingCartService, IGTMService gtmService, IUrlRecordService urlRecordService, IPriceCalculationService priceCalculationService, ICurrencyService currencyService)
	{
		_googleTagManagerConfigurationSettings = googleTagManagerConfigurationSettings;
		_productService = productService;
		_workContext = workContext;
		_storeContext = storeContext;
		_nopStationContext = nopStationContext;
		_orderSettings = orderSettings;
		_orderTotalCalculationService = orderTotalCalculationService;
		_settingService = settingService;
		_httpContextAccessor = httpContextAccessor;
		_shoppingCartService = shoppingCartService;
		_gtmService = gtmService;
		_urlRecordService = urlRecordService;
		_priceCalculationService = priceCalculationService;
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

	private async Task<string> GetGTMScriptByZoneAsync(string widgetZones)
	{
		string gtmScript = "";
		if (widgetZones == PublicWidgetZones.HeadHtmlTag)
		{
			gtmScript = GoogleTagManagerDefaults.HeadTrackingScript.Replace("%GTMCONTAINERID%", _googleTagManagerConfigurationSettings.GTMContainerId);
			RouteData routeData = ((ViewComponent)this).Url.ActionContext.RouteData;
			string controller = routeData.Values["controller"].ToString();
			string action = routeData.Values["action"].ToString();
			Customer customer = await _workContext.GetCurrentCustomerAsync();
			Store store = await _storeContext.GetCurrentStoreAsync();
			Currency currentCurrency = await _workContext.GetWorkingCurrencyAsync();
			string currencyCode = currentCurrency.CurrencyCode;
			if (controller == null || action == null)
			{
				gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", "");
			}
			else if (controller.Equals("Product", StringComparison.InvariantCultureIgnoreCase) && action.Equals("ProductDetails", StringComparison.InvariantCultureIgnoreCase))
			{
				int productId = Convert.ToInt32(routeData.Values["productid"].ToString());
				Product product = await _productService.GetProductByIdAsync(productId);
				decimal item = (await _priceCalculationService.GetFinalPriceAsync(product, customer, store, 0m, true, product.OrderMinimumQuantity)).Item1;
				string viewItemScript = GoogleTagManagerDefaults.BaseEventScript;
				string text = product.Sku;
				if (string.IsNullOrEmpty(text))
				{
					text = productId.ToString();
				}
				viewItemScript = viewItemScript.Replace("%product_ids%", "'" + FixIllegalJavaScriptChars(text) + "'");
				viewItemScript = viewItemScript.Replace("%page_type%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.PRODUCT));
				viewItemScript = viewItemScript.Replace("%event_name%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.VIEW_ITEM));
				decimal price = item * (decimal)product.OrderMinimumQuantity;
				price = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(price, currentCurrency);
				decimal unitPrice = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(product.Price, currentCurrency);
				viewItemScript = viewItemScript.Replace("%value%", price.ToString("0.00", CultureInfo.InvariantCulture));
				viewItemScript = viewItemScript.Replace("%currency%", FixIllegalJavaScriptChars(currencyCode));
				string text2 = viewItemScript;
				viewItemScript = text2.Replace("%productInformation%", await _gtmService.GetProductDetailsAsync(productId, product.OrderMinimumQuantity));
				viewItemScript = viewItemScript.Replace("%unitPrice%", FixIllegalJavaScriptChars(unitPrice.ToString("0.00", CultureInfo.InvariantCulture)));
				gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", viewItemScript);
			}
			else
			{
				bool flag = (await _nopStationContext.GetRouteNameAsync()).Equals("CheckoutOnePage", StringComparison.InvariantCultureIgnoreCase);
				if (!flag)
				{
					flag = (await _nopStationContext.GetRouteNameAsync()).Equals("CheckoutBillingAddress", StringComparison.InvariantCultureIgnoreCase);
				}
				if (flag)
				{
					int num = await _storeContext.GetActiveStoreScopeConfigurationAsync();
					GoogleTagManagerSettings googleTagManagerSettings = await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(num);
					if (!_orderSettings.CheckoutDisabled && googleTagManagerSettings.IsEnable)
					{
						IList<ShoppingCartItem> cart = await _shoppingCartService.GetShoppingCartAsync(customer, (ShoppingCartType?)(ShoppingCartType)1, ((BaseEntity)store).Id, (int?)null, (DateTime?)null, (DateTime?)null);
						if (cart.Any())
						{
							decimal unitPrice = (await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false)).Item3;
							unitPrice = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(unitPrice, currentCurrency);
							string viewItemScript = GoogleTagManagerDefaults.BaseEventScript;
							StringBuilder ids = new StringBuilder();
							foreach (ShoppingCartItem item2 in cart)
							{
								if (ids.Length > 0)
								{
									ids.Append(",");
								}
								StringBuilder stringBuilder = ids;
								stringBuilder.AppendLine("'" + await _gtmService.GetProductIdAsync(item2.ProductId) + "'");
							}
							viewItemScript = viewItemScript.Replace("%currency%", FixIllegalJavaScriptChars(currencyCode));
							viewItemScript = viewItemScript.Replace("%event_name%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.BEGIN_CHECKOUT));
							viewItemScript = viewItemScript.Replace("%page_type%", FixIllegalJavaScriptChars(GoogleTagManagerDefaults.CHECKOUT_PAGE));
							viewItemScript = viewItemScript.Replace("%value%", unitPrice.ToString("0.00", CultureInfo.InvariantCulture));
							viewItemScript = viewItemScript.Replace("%product_ids%", ids.ToString());
							viewItemScript = viewItemScript.Replace("%productInformation%", await _gtmService.PrepareProductItemsAsync(cart));
							gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", viewItemScript);
						}
					}
				}
				else if ((await _nopStationContext.GetRouteNameAsync()).Equals("ShoppingCart", StringComparison.InvariantCultureIgnoreCase))
				{
					int productId = ((BaseEntity)(await _storeContext.GetCurrentStoreAsync())).Id;
					GoogleTagManagerSettings googleTagManagerSettings2 = await _settingService.LoadSettingAsync<GoogleTagManagerSettings>(productId);
					if (!_orderSettings.CheckoutDisabled && googleTagManagerSettings2.IsEnable)
					{
						gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", await _gtmService.PrepareShoppingCartScriptAsync(customer, productId));
					}
				}
				else
				{
					string key = string.Format(GoogleTagManagerDefaults.SessionKey, customer.CustomerGuid.ToString());
					string text3 = _httpContextAccessor.HttpContext.Session.GetString(key);
					if (!string.IsNullOrEmpty(text3))
					{
						gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", text3);
						_httpContextAccessor.HttpContext.Session.Remove(key);
					}
					else
					{
						gtmScript = gtmScript.Replace("%TRACKINGINFORMATION%", "");
					}
				}
			}
		}
		else if (widgetZones == PublicWidgetZones.BodyStartHtmlTagAfter)
		{
			gtmScript = GoogleTagManagerDefaults.BodyTrackingScript;
			gtmScript = gtmScript.Replace("%GTMCONTAINERID%", _googleTagManagerConfigurationSettings.GTMContainerId);
		}
		return gtmScript;
	}

	public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
	{
		if (!_googleTagManagerConfigurationSettings.IsEnable)
		{
			return ((NopStationViewComponent)this).Content("");
		}
		string text = _httpContextAccessor.HttpContext.Request.GetDisplayUrl().Split('/')[^1];
		string pageType = "home";
		if (!string.IsNullOrEmpty(text))
		{
			UrlRecord val = await _urlRecordService.GetBySlugAsync(text);
			if (val != null)
			{
				pageType = val.EntityName;
			}
		}
		PublicInfoModel publicInfoModel = new PublicInfoModel();
		PublicInfoModel publicInfoModel2 = publicInfoModel;
		publicInfoModel2.Script = await GetGTMScriptByZoneAsync(widgetZone);
		publicInfoModel2.RenderAntiForgeryToken = widgetZone == PublicWidgetZones.BodyStartHtmlTagAfter;
		PublicInfoModel publicInfoModel3 = publicInfoModel;
		((ViewComponent)this).ViewData["pageType"] = pageType;
		return ((NopStationViewComponent)this).View<PublicInfoModel>("~/Plugins/NopStation.Plugin.Widgets.GoogleTagManager/Views/Shared/Components/GoogleTagManager/Default.cshtml", publicInfoModel3);
	}
}
