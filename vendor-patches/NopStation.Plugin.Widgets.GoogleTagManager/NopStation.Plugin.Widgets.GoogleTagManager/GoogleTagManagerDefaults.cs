namespace NopStation.Plugin.Widgets.GoogleTagManager;

public class GoogleTagManagerDefaults
{
	public static string HeadTrackingScript = "<!-- NS Google Tag Manager (script) -->\r\n                                        <script>\r\n                                        window.dataLayer = window.dataLayer || []; \r\n                                        dataLayer.push({%TRACKINGINFORMATION%});\r\n                                      </script>\r\n                                      <script>\r\n                                            (function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start': new Date().getTime(),event:'gtm.js'});\r\n                                            var f=d.getElementsByTagName(s)[0], j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';\r\n                                            j.async=true;j.src= 'https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f); })\r\n                                            (window,document,'script','dataLayer','%GTMCONTAINERID%');\r\n                                      </script>";

	public static string BodyTrackingScript = "<!-- NS Google Tag Manager (noscript) -->\r\n                                        <noscript><iframe src=\"https://www.googletagmanager.com/ns.html?id=%GTMCONTAINERID%\"\r\n                                        height=\"0\" width=\"0\" style=\"display:none;visibility:hidden\"></iframe></noscript>\r\n                                        <!-- End Google Tag Manager (noscript) -->";

	public static string BaseEventScript = "\r\n                             'event': '%event_name%', \r\n                             'var_prodid': [%product_ids%],\r\n                             'var_pagetype' : '%page_type%',\r\n                             'var_prodval':%value%,\r\n                             'ecommerce':{\r\n                                            'currency': '%currency%',\r\n                                            'value': %value%,\r\n                                            'items': [%productInformation%] \r\n                                         }";

	public static string SessionKey => "NopStation.GMTSession{0}";

	public static string REMOVE_TO_CART => "remove_from_cart";

	public static string SEARCH => "search";

	public static string CATEGORY_VIEW => "category";

	public static string CUSTOMER_REGISTER => "customer_registered";

	public static string VIEW_ITEM => "view_item";

	public static string HOME_PAGE => "home_page_visit";

	public static string VIEW_CART => "view_cart";

	public static string PRODUCT => "product";

	public static string BEGIN_CHECKOUT => "begin_checkout";

	public static string CHECKOUT_PAGE => "checkout";

	public static string CART_PAGE => "cart";

	public static string PURCHASE => "purchase";

	public static string VIEW_ITEM_LIST => "view_item_list";

	public static string CONTACT_US => "contact_us";
}
