$(document).ready(function () {
  NSGoogleTagManager.init()
});

var NSGoogleTagManager = {
  pendingAddToCart: null,
  init: function () {
    this.installAjaxCartSuccessHook(0);

    // Capture only. A supported cart success callback below is the sole
    // add_to_cart emission gate, so unrelated AJAX activity cannot create one.
    $(document)
      .off('click.nopStationGtmAddToCart', '.add-to-cart-button, .product-box-add-to-cart-button')
      .on('click.nopStationGtmAddToCart', '.add-to-cart-button, .product-box-add-to-cart-button', function (e) {
        if ($(this).hasClass('add-to-wishlist-button') || $(this).hasClass('miniProductDetailsViewAddToWishlistButton')) {
          NSGoogleTagManager.pendingAddToCart = null;
          return;
        }
        var card = this.closest('.product-item');
        var productId = this.dataset.productid || (card && card.dataset.productid) || '';
        NSGoogleTagManager.pendingAddToCart = {
          element: e,
          details: !$(this).hasClass('product-box-add-to-cart-button'),
          productId: String(productId)
        };
      });

    // The active Element theme uses SevenSpikes NopAjaxCart instead of core
    // AjaxCart.success_process. Its success-only event is raised after the
    // server accepted the add and the visible cart quantity was updated.
    $(document)
      .off('nopAjaxCartProductAddedToCartEvent.nopStationGtmAddToCart')
      .on('nopAjaxCartProductAddedToCartEvent.nopStationGtmAddToCart', function (e) {
        NSGoogleTagManager.completePendingAddToCart(e && e.productId);
      });

    // Wishlist remains distinct and never enters the add_to_cart success path.
    $('.add-to-wishlist-button').click(function (e) {
      var isAddToWishlistClicked = false;
      $(document).ajaxStop(function () {
        if (!isAddToWishlistClicked) {
          isAddToWishlistClicked = true;
          NSGoogleTagManager.addToWishlistCicked(e, true);
        }
      });
    });
  },

  installAjaxCartSuccessHook: function (retries) {
    if (!window.AjaxCart || typeof AjaxCart.success_process !== 'function') {
      if (retries < 50) window.setTimeout(function () { NSGoogleTagManager.installAjaxCartSuccessHook(retries + 1); }, 50);
      return;
    }
    if (AjaxCart.__nopStationGtmAddToCartHook) return;
    var originalSuccess = AjaxCart.success_process;
    AjaxCart.success_process = function (response) {
      var result = originalSuccess.apply(this, arguments);
      var pending = NSGoogleTagManager.pendingAddToCart;
      var cartUpdated = response && (response.updatetopcartsectionhtml || response.updateflyoutcartsectionhtml);
      if (pending && response && response.success === true && cartUpdated)
        NSGoogleTagManager.completePendingAddToCart();
      else if (pending)
        NSGoogleTagManager.pendingAddToCart = null;
      return result;
    };
    AjaxCart.__nopStationGtmAddToCartHook = true;
  },

  completePendingAddToCart: function (productId) {
    var pending = NSGoogleTagManager.pendingAddToCart;
    if (!pending) return;
    if (productId && pending.productId && String(productId) !== pending.productId) return;
    NSGoogleTagManager.pendingAddToCart = null;
    NSGoogleTagManager.addToCartCicked(pending.element, pending.details, pending.productId);
  },

  clearDataLayer: function () {
    dataLayer.push(function () {
      this.reset();
    });
  },
  addToCartCicked: function (elem, flag, capturedProductId) {
    window.dataLayer = window.dataLayer || [];
    dataLayer.push({
      ecommerce: null
    });
    var productId = capturedProductId || elem.currentTarget.dataset.productid;
    if (!productId)
      productId = elem.currentTarget.closest('.product-item').dataset.productid;
    if (!productId) return;
    var quantity = 0;
    if ($('.qty-input').length > 0)
      quantity = $('.qty-input').val();

    $.ajax({
      cache: false,
      type: "GET",
      url: "/GtmEventSend/ProductDetails?productId=" + productId + "&isClickedFromProductDetailsPage=" + flag + "&quantity=" + quantity,
      success: function (val, textStatus, jqXHR) {
        if (!val.Result)
          return;
        var product = {
          'item_id': val.Data.Sku,
          'item_name': val.Data.Name,
          'affiliation': val.Data.Affiliation,
          'coupon': val.Data.Coupon,
          'discount': val.Data.Discount,
          'index': val.Data.Index,
          'item_brand': val.Data.Manufacturer,
          'price': val.Data.Price,
          'quantity': val.Data.Copy,
          'copy': val.Data.Copy
        };

        for (var i = 0; i < val.Data.Categories.length; i++) {
          var categoryKey = 'item_category'
          if (i > 0) categoryKey = categoryKey + (i + 1);
          product[categoryKey] = val.Data.Categories[i];
        }
        var items = [product];
        dataLayer.push({
          event: 'add_to_cart',
          'var_prodid': [val.Data.Sku],
          'var_pagetype': 'product',
          "var_prodval": val.Data.Price,
          'ecommerce': {
            'currency': val.Data.Currency,
            'value': val.Data.Price,
            'items': items
          }
        });
        NSGoogleTagManager.clearDataLayer();
      },
    });
  },

  addToWishlistCicked: function (elem, flag) {
    window.dataLayer = window.dataLayer || [];
    dataLayer.push({
      ecommerce: null
    });
    var productId = elem.currentTarget.dataset.productid;
    if (!productId) {
      productId = elem.currentTarget.closest('.product-item').dataset.productid;
      flag = false;
    }
    if (!productId) return;
    var shoppingCart = false;
    var quantity = 0;
    if ($('.qty-input').length > 0)
      quantity = $('.qty-input').val();
    $.ajax({
      cache: false,
      type: "GET",
      url: "/GtmEventSend/ProductDetails?productId=" + productId + "&isClickedFromProductDetailsPage=" + flag + "&quantity=" + quantity + "&isShoppingCart=" + shoppingCart,
      success: function (val, textStatus, jqXHR) {
        if (!val.Result)
          return;
        var product = {
          'item_id': val.Data.Sku,
          'item_name': val.Data.Name,
          'affiliation': val.Data.Affiliation,
          'coupon': val.Data.Coupon,
          'discount': val.Data.Discount,
          'index': val.Data.Index,
          'item_brand': val.Data.Manufacturer,
          'price': val.Data.Price,
          'quantity': val.Data.Copy,
          'copy': val.Data.Copy
        };

        for (var i = 0; i < val.Data.Categories.length; i++) {
          var categoryKey = 'item_category'
          if (i > 0) categoryKey = categoryKey + (i + 1);
          product[categoryKey] = val.Data.Categories[i];
        }
        var items = [product];
        dataLayer.push({
          event: 'add_to_wishlist',
          'var_prodid': [val.Data.Sku],
          'var_pagetype': 'product',
          "var_prodval": val.Data.Price,
          'ecommerce': {
            'currency': val.Data.Currency,
            'value': val.Data.Price,
            'items': items
          }
        });
        NSGoogleTagManager.clearDataLayer();
      },
    });
  },
};

$(document).ready(function () {
  function processProducts(products, promotion = false) {
    var items = [];

    for (var i = 0; i < products.length; i++) {
      var data = products[i];
      var product = {
        'item_id': data.Sku,
        'item_name': data.Name,
        'affiliation': data.Affiliation,
        'coupon': data.Coupon,
        'currency': data.CurrencyCode,
        'discount': data.Discount,
        'index': data.Index,
        'item_brand': data.Brand,
        'item_list_id': data.ItemListId,
        'item_list_name': data.ItemListName,
        'price': data.Price,
        'quantity': data.Quantity,
      };

      for (var j = 0; j < data.Categories.length; j++) {
        var categoryKey = 'item_category'
        if (j > 0) categoryKey = categoryKey + (j + 1);
        product[categoryKey] = data.Categories[j];
      }
      if (promotion) product['location_id'] = "";
      items.push(product);
    }

    return items;
  }

  function postData(data) {
    data = data || {};
    var token = $("input[name='__RequestVerificationToken']").first().val();
    if (!token) return null;
    data.__RequestVerificationToken = token;
    return data;
  }

  //newsletter subscribe-event
  const button = document.querySelector('.newsletter-subscribe-button');
  if (button) {
    button.addEventListener('click', function () {
      var emailInput = document.querySelector('.newsletter-subscribe-text');
      var email = emailInput.value;
      var regex = /^([a-zA-Z0-9_\-\.]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})$/;
      if (regex.test(email)) {
        dataLayer.push({
          event: 'newsletter_subscription',
          'source': 'form_footer'
        });
        clearDataLayer();
      }
    });
  }

  // Capture on the delegated click, emit only after nopCommerce accepted the
  // selected shipping method in ShippingMethod.nextStep.
  var shippingInfoPending = false;
  var lastShippingSelection = null;
  var pendingShippingSelection = null;
  function captureShippingSelection() {
    var option = $("input[name='shippingoption']:checked").first();
    pendingShippingSelection = option.length ? String(option.val() || '') : null;
  }
  function emitAcceptedShippingInfo(selection) {
    if (!selection || selection === lastShippingSelection || shippingInfoPending) return;
    var shippingName = selection.split("___");
    var systemName = shippingName.length > 1 ? shippingName[1] : '';
    var request = postData({ systemName: systemName });
    if (!request) return;
    shippingInfoPending = true;
    $.ajax({
      cache: false,
      type: "POST",
      url: "/GtmEventSend/ShoppingCartDetails",
      data: request,
      success: function (val, textStatus, jqXHR) {
        if (!val.Result)
          return;
        var items = processProducts(val.Products);
        clearDataLayer();
        dataLayer.push({
          event: 'add_shipping_info',
          'var_prodid': val.ProductIds,
          'var_currency': val.Currency,
          'ecommerce': {
            'currency': val.Currency,
            'value': val.Total,
            'coupon': "",
            'shipping_tier': shippingName[0],
            'items': items
          }
        });
        lastShippingSelection = selection;
        clearDataLayer();
      },
      complete: function () { shippingInfoPending = false; }
    });
  }
  function installShippingSuccessHook(retries) {
    if (!window.ShippingMethod || typeof ShippingMethod.save !== 'function' || typeof ShippingMethod.nextStep !== 'function') {
      if (retries < 50) window.setTimeout(function () { installShippingSuccessHook(retries + 1); }, 50);
      return;
    }
    if (ShippingMethod.__nopStationGtmShippingHook) return;
    var originalSave = ShippingMethod.save;
    var originalNextStep = ShippingMethod.nextStep;
    ShippingMethod.save = function () {
      captureShippingSelection();
      return originalSave.apply(this, arguments);
    };
    ShippingMethod.nextStep = function (response) {
      var selection = pendingShippingSelection;
      pendingShippingSelection = null;
      if (response && !response.error)
        emitAcceptedShippingInfo(selection);
      return originalNextStep.apply(this, arguments);
    };
    ShippingMethod.__nopStationGtmShippingHook = true;
  }
  $(document)
    .off('click.nopStationGtmShipping', '.shipping-method-next-step-button')
    .on('click.nopStationGtmShipping', '.shipping-method-next-step-button', captureShippingSelection);
  installShippingSuccessHook(0);

  var pendingPaymentSelection = null;
  var lastPaymentSelection = null;
  var paymentInfoPending = false;
  function capturePaymentSelection() {
    var option = $("input[name='paymentmethod']:checked").first();
    pendingPaymentSelection = option.length ? String(option.val() || '') : null;
  }
  function emitAcceptedPaymentInfo(selection) {
    if (!selection || selection === lastPaymentSelection || paymentInfoPending) return;
    var request = postData({ systemName: selection });
    if (!request) return;
    paymentInfoPending = true;
    $.ajax({
      cache: false,
      type: "POST",
      url: "/GtmEventSend/ShoppingCartDetails",
      data: request,
      success: function (val, textStatus, jqXHR) {
        if (!val.Result)
          return;
        var items = processProducts(val.Products);
        dataLayer.push({
          event: 'add_payment_info',
          'var_prodid': val.ProductIds,
          'var_currency': val.Currency,
          'ecommerce': {
            'currency': val.Currency,
            'value': val.Total,
            'coupon': "",
            'payment_type': val.Name,
            'items': items
          }
        });
        lastPaymentSelection = selection;
        clearDataLayer();
      },
      complete: function () { paymentInfoPending = false; }
    });
  }
  function installPaymentSuccessHook(retries) {
    if (!window.PaymentMethod || typeof PaymentMethod.save !== 'function' || typeof PaymentMethod.nextStep !== 'function') {
      if (retries < 50) window.setTimeout(function () { installPaymentSuccessHook(retries + 1); }, 50);
      return;
    }
    if (PaymentMethod.__nopStationGtmPaymentHook) return;
    var originalSave = PaymentMethod.save;
    var originalNextStep = PaymentMethod.nextStep;
    PaymentMethod.save = function () {
      capturePaymentSelection();
      return originalSave.apply(this, arguments);
    };
    PaymentMethod.nextStep = function (response) {
      var selection = pendingPaymentSelection;
      pendingPaymentSelection = null;
      if (response && !response.error)
        emitAcceptedPaymentInfo(selection);
      return originalNextStep.apply(this, arguments);
    };
    PaymentMethod.__nopStationGtmPaymentHook = true;
  }
  $(document)
    .off('click.nopStationGtmPayment', '.payment-method-next-step-button')
    .on('click.nopStationGtmPayment', '.payment-method-next-step-button', capturePaymentSelection);
  installPaymentSuccessHook(0);

  function clearDataLayer() {
    dataLayer.push(function () {
      this.reset();
    });
  }

  var pageType = $(".page_type_gtm").data("page-type");
  var productItemsByProductId = Object.create(null);

  function cacheProductItems(productIds, items) {
    for (var i = 0; i < productIds.length && i < items.length; i++)
      productItemsByProductId[String(productIds[i])] = items[i];
  }

  // Product-list navigation can cancel an analytics request before it leaves
  // the browser. Hydrate truthful GA4 item data up front, then let GTM finish
  // the select_item tag (or hit the short timeout) before same-tab navigation.
  $(document)
    .off('click.nopStationGtmSelectItem', '.product-item .product-title a, .product-item .picture a')
    .on('click.nopStationGtmSelectItem', '.product-item .product-title a, .product-item .picture a', function (event) {
      if (event.isDefaultPrevented() || event.which !== 1 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
        return;

      var link = this;
      if (!link.href || (link.target && link.target.toLowerCase() !== '_self') || link.hasAttribute('download'))
        return;

      var card = link.closest('.product-item');
      var productId = card && card.getAttribute('data-productid');
      var item = productId && productItemsByProductId[String(productId)];
      if (!item)
        return;

      event.preventDefault();
      var navigated = false;
      var continueNavigation = function () {
        if (navigated) return;
        navigated = true;
        window.location.assign(link.href);
      };

      dataLayer.push({ ecommerce: null });
      dataLayer.push({
        event: 'select_item',
        eventCallback: continueNavigation,
        eventTimeout: 500,
        ecommerce: {
          item_list_id: item.item_list_id,
          item_list_name: item.item_list_name || document.title || undefined,
          items: [item]
        }
      });
      window.setTimeout(continueNavigation, 550);
    });

  const productItemDivs = document.querySelectorAll('div.product-item');
  if (productItemDivs && productItemDivs.length) {
    const productIdList = [];
    productItemDivs.forEach(div => {
      const productId = div.getAttribute('data-productid');
      if (productId) {
        productIdList.push(productId);
      }
    });
    var request = postData({ productIds: productIdList });
    if (!request) return;
    $.ajax({
      cache: false,
      type: "POST",
      url: "/GtmEventSend/GetProducts",
      data: request,
      success: function (val, textStatus, jqXHR) {
        if (!val.Result || productIdList.length === 0)
          return;

        var items = processProducts(val.Products);
        cacheProductItems(productIdList, items);
        // Category view_item_list is supplied server-side by the same plugin.
        // The request above is still needed there to hydrate select_item data.
        if (pageType === 'Category')
          return;
        dataLayer.push({
          event: 'view_item_list',
          'var_prodid': val.ProductIds,
          'var_pagetype': pageType,
          'ecommerce': {
            'items': items,
          }
        });
        clearDataLayer();
      },
    });
  }
});
