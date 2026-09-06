const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const sourcePath = path.join(__dirname, '..', 'Content', 'js', 'ns.gtm.js');
const source = fs.readFileSync(sourcePath, 'utf8');
const builtSourcePath = path.join(__dirname, '..', 'bin', 'Release', 'net9.0', 'Content', 'js', 'ns.gtm.js');
const viewPath = path.join(__dirname, '..', 'Views', 'Shared', 'Components', 'GoogleTagManager', 'Default.cshtml');
const view = fs.readFileSync(viewPath, 'utf8');
const builtViewPath = path.join(__dirname, '..', 'bin', 'Release', 'net9.0', 'Views', 'Shared', 'Components', 'GoogleTagManager', 'Default.cshtml');

function createHarness({ productResult = true } = {}) {
  const handlers = new Map();
  const ajaxCalls = [];
  const dataLayer = [];
  const document = {};

  function jquery(subject) {
    const api = {
      length: subject === '.qty-input' ? 1 : 0,
      ready(callback) {
        handlers.set('ready', { callback });
        return api;
      },
      off(eventName) {
        handlers.delete(eventName.split('.')[0]);
        return api;
      },
      on(eventName, selectorOrHandler, maybeHandler) {
        handlers.set(eventName.split('.')[0], {
          handler: typeof selectorOrHandler === 'function' ? selectorOrHandler : maybeHandler,
          selector: typeof selectorOrHandler === 'string' ? selectorOrHandler : undefined
        });
        return api;
      },
      click(callback) {
        handlers.set('wishlist-click', { handler: callback });
        return api;
      },
      ajaxStop() {
        return api;
      },
      hasClass(className) {
        return Boolean(subject && subject.classes && subject.classes.includes(className));
      },
      val() {
        return '1';
      }
    };
    return api;
  }

  jquery.ajax = function (options) {
    ajaxCalls.push(options);
    if (options.url.startsWith('/GtmEventSend/ProductDetails')) {
      options.success({
        Result: productResult,
        Data: {
          Sku: 'SKU-50',
          Name: 'Product 50',
          Affiliation: 'Hood Archery Shop',
          Coupon: '',
          Discount: 0,
          Index: 0,
          Manufacturer: 'Hood',
          Price: 10,
          Copy: 1,
          Categories: [],
          Currency: 'USD'
        }
      });
    }
    return {
      always() { return this; },
      done() { return this; }
    };
  };

  const window = {
    dataLayer,
    setTimeout() {}
  };
  const context = vm.createContext({
    console,
    dataLayer,
    document,
    jQuery: jquery,
    window,
    $: jquery
  });
  vm.runInContext(source, context, { filename: sourcePath });
  context.NSGoogleTagManager.init();

  const button = {
    classes: ['add-to-cart-button', 'nopAjaxCartProductVariantAddToCartButton'],
    dataset: { productid: '50' },
    closest() { return null; }
  };

  function captureProductDetailClick() {
    const clickEvent = { currentTarget: button };
    const clickHandler = handlers.get('click');
    assert.equal(clickHandler.selector, '.add-to-cart-button, .product-box-add-to-cart-button');
    clickHandler.handler.call(button, clickEvent);
    return clickEvent;
  }

  function signalSevenSpikesSuccess(productId = 50) {
    handlers.get('nopAjaxCartProductAddedToCartEvent').handler({ productId, quantity: 1 });
  }

  return {
    ajaxCalls,
    button,
    captureProductDetailClick,
    context,
    dataLayer,
    document,
    signalSevenSpikesSuccess
  };
}

function addToCartEvents(harness) {
  return harness.dataLayer.filter(entry => entry && entry.event === 'add_to_cart');
}

function contentVersion(content) {
  return crypto.createHash('sha256').update(content).digest('base64url');
}

test('plugin view registers ns.gtm.js with its content hash in the URL', () => {
  const versionMatch = view.match(/ns\.gtm\.js\?v=([^"&]+)"/);

  assert.ok(versionMatch, 'the registered script URL must contain a version query');
  assert.equal(versionMatch[1], contentVersion(source));
  assert.match(view, /<script asp-location="Footer" src="~\/Plugins\/NopStation\.Plugin\.Widgets\.GoogleTagManager\/Content\/js\/ns\.gtm\.js\?v=[^"&]+"><\/script>/);
});

test('content version changes with JS content and source matches the Release build', () => {
  const builtSource = fs.readFileSync(builtSourcePath, 'utf8');
  const builtView = fs.readFileSync(builtViewPath, 'utf8');

  assert.equal(builtSource, source);
  assert.equal(builtView, view);
  assert.equal(contentVersion(builtSource), contentVersion(source));
  assert.notEqual(contentVersion(`${source}\n// changed`), contentVersion(source));
});

test('PDP add emits once after the SevenSpikes success event even after click dispatch ends', () => {
  const harness = createHarness();
  const clickEvent = harness.captureProductDetailClick();

  assert.equal(harness.ajaxCalls.length, 0, 'the click alone must not hydrate or emit');

  // jQuery click event objects are dispatch-scoped. The async success path must
  // use the stable product id captured at click time, not currentTarget later.
  clickEvent.currentTarget = harness.document;
  harness.signalSevenSpikesSuccess();

  assert.equal(harness.ajaxCalls.length, 1);
  assert.match(harness.ajaxCalls[0].url, /productId=50(?:&|$)/);
  assert.equal(addToCartEvents(harness).length, 1);
});

test('PDP click without a SevenSpikes success event emits nothing', () => {
  const harness = createHarness();
  harness.captureProductDetailClick();

  assert.equal(harness.ajaxCalls.length, 0);
  assert.equal(addToCartEvents(harness).length, 0);
});

test('mismatched SevenSpikes product success is suppressed', () => {
  const harness = createHarness();
  harness.captureProductDetailClick();
  harness.signalSevenSpikesSuccess(51);

  assert.equal(harness.ajaxCalls.length, 0);
  assert.equal(addToCartEvents(harness).length, 0);
});

test('duplicate SevenSpikes success signals hydrate and emit only once', () => {
  const harness = createHarness();
  const clickEvent = harness.captureProductDetailClick();
  clickEvent.currentTarget = harness.document;

  harness.signalSevenSpikesSuccess();
  harness.signalSevenSpikesSuccess();

  assert.equal(harness.ajaxCalls.length, 1);
  assert.equal(addToCartEvents(harness).length, 1);
});

test('failed GTM product hydration suppresses add_to_cart', () => {
  const harness = createHarness({ productResult: false });
  const clickEvent = harness.captureProductDetailClick();
  clickEvent.currentTarget = harness.document;
  harness.signalSevenSpikesSuccess();

  assert.equal(harness.ajaxCalls.length, 1);
  assert.equal(addToCartEvents(harness).length, 0);
});
