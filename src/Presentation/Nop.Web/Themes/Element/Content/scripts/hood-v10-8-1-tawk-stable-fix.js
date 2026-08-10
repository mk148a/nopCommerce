(function () {
  'use strict';

  window.HOOD_V1081_TAWK_STABLE_FIX_VERSION = '10.8.1';

  var DEFAULT_TAWK_SRC = 'https://embed.tawk.to/5d69a40f77aa790be331ab24/default';
  var cfg = window.HOOD_TP_CONFIG || {};
  var tawkSrc = cfg.tawkSrc || DEFAULT_TAWK_SRC;

  /* FooterCustomHtml can arrive after this footer script.  Re-read it during
     init so the configured property URL is used without hardcoding it in the
     theme asset. */
  function refreshConfig() {
    cfg = window.HOOD_TP_CONFIG || {};
    tawkSrc = cfg.tawkSrc || DEFAULT_TAWK_SRC;
  }

  var state = {
    loading: false,
    loaded: false,
    pendingOpen: false,
    lastOpenReason: '',
    duplicateScriptsRemoved: 0
  };

  function log() {
    if (cfg.debug && window.console && console.log) {
      console.log.apply(console, ['[HOOD V10.8.1]'].concat([].slice.call(arguments)));
    }
  }

  function installTelemetryGuard() {
    if (window.HOOD_V1081_TAWK_TELEMETRY_GUARD) return;
    window.HOOD_V1081_TAWK_TELEMETRY_GUARD = true;

    if (!window.fetch) return;

    var nativeFetch = window.fetch.bind(window);

    window.fetch = function (input, init) {
      var url = (typeof input === 'string') ? input : (input && input.url ? input.url : '');
      url = String(url || '');

      /*
        This is Tawk performance telemetry. It often fails CORS and it is not required for chat.
        Return 204 with NULL body. 204/205/304 cannot have a body in modern browsers.
      */
      if (url.indexOf('va.tawk.to/log-performance/v3') >= 0) {
        return Promise.resolve(new Response(null, {
          status: 204,
          statusText: 'No Content'
        }));
      }

      return nativeFetch(input, init);
    };
  }

  function removeDuplicateTawkScripts() {
    var scripts = Array.prototype.slice.call(document.querySelectorAll('script[src*="embed.tawk.to"]'));
    if (scripts.length <= 1) return 0;

    var removed = 0;
    scripts.slice(1).forEach(function (s) {
      try {
        s.parentNode && s.parentNode.removeChild(s);
        removed++;
      } catch (e) {}
    });

    state.duplicateScriptsRemoved += removed;
    return removed;
  }

  function hasTawkScript() {
    return !!document.querySelector('script[src*="embed.tawk.to"]');
  }

  function isTawkReady() {
    return !!(window.Tawk_API && (
      typeof window.Tawk_API.maximize === 'function' ||
      typeof window.Tawk_API.toggle === 'function' ||
      typeof window.Tawk_API.showWidget === 'function'
    ));
  }

  function tryMaximize() {
    try {
      if (!window.Tawk_API) return false;

      if (typeof window.Tawk_API.showWidget === 'function') {
        window.Tawk_API.showWidget();
      }

      if (typeof window.Tawk_API.maximize === 'function') {
        window.Tawk_API.maximize();
        state.pendingOpen = false;
        state.loaded = true;
        return true;
      }

      if (typeof window.Tawk_API.toggle === 'function') {
        window.Tawk_API.toggle();
        state.pendingOpen = false;
        state.loaded = true;
        return true;
      }
    } catch (e) {
      console.warn('[HOOD V10.8.1] Tawk maximize failed:', e);
    }

    return false;
  }

  function waitAndOpen(tries) {
    tries = tries || 0;

    removeDuplicateTawkScripts();

    if (isTawkReady()) {
      tryMaximize();
      return;
    }

    if (tries > 100) {
      console.warn('[HOOD V10.8.1] Tawk API was not ready after waiting.');
      return;
    }

    setTimeout(function () {
      waitAndOpen(tries + 1);
    }, 150);
  }

  function installOnLoadHook() {
    window.Tawk_API = window.Tawk_API || {};

    if (window.Tawk_API.__hood_v1081_onload_hooked) return;
    window.Tawk_API.__hood_v1081_onload_hooked = true;

    var previousOnLoad = window.Tawk_API.onLoad;

    window.Tawk_API.onLoad = function () {
      state.loaded = true;

      if (typeof previousOnLoad === 'function') {
        try { previousOnLoad.apply(window.Tawk_API, arguments); } catch (e) {}
      }

      if (state.pendingOpen) {
        setTimeout(tryMaximize, 100);
        setTimeout(tryMaximize, 500);
        setTimeout(tryMaximize, 1200);
      }
    };
  }

  function injectTawkScript() {
    installTelemetryGuard();
    installOnLoadHook();
    removeDuplicateTawkScripts();

    if (hasTawkScript()) {
      waitAndOpen(0);
      return;
    }

    if (state.loading) {
      waitAndOpen(0);
      return;
    }

    state.loading = true;
    window.Tawk_LoadStart = window.Tawk_LoadStart || new Date();

    var s = document.createElement('script');
    s.async = true;
    s.src = tawkSrc;
    s.charset = 'UTF-8';
    s.setAttribute('crossorigin', '*');

    s.onload = function () {
      state.loading = false;
      state.loaded = true;
      waitAndOpen(0);
    };

    s.onerror = function () {
      state.loading = false;
      console.warn('[HOOD V10.8.1] Tawk script failed to load:', tawkSrc);
    };

    (document.head || document.documentElement).appendChild(s);
  }

  function openTawk(reason) {
    state.pendingOpen = true;
    state.lastOpenReason = reason || 'manual';

    installTelemetryGuard();
    installOnLoadHook();

    if (isTawkReady()) {
      tryMaximize();
      return true;
    }

    injectTawkScript();

    setTimeout(tryMaximize, 500);
    setTimeout(tryMaximize, 1200);
    setTimeout(tryMaximize, 2500);

    return true;
  }

  function isOurChatButton(el) {
    if (!el || el.nodeType !== 1) return false;

    /*
      Narrow selector. Do not catch every ".chat" inside Tawk's own widget.
      This targets our lazy green button / wrapper only.
    */
    var direct = el.closest(
      '#hood-tawk-button,' +
      '.hood-tawk-button,' +
      '.hood-tawk-chat-button,' +
      '.hood-chat-button,' +
      '[data-hood-tawk],' +
      '[data-tawk-open]'
    );

    if (direct) return true;

    /*
      Fallback for the simple green text button that says only "Chat".
      To avoid hijacking unrelated site content, require fixed/sticky/absolute positioning
      or bottom/right placement.
    */
    var candidate = el.closest('button,a,div,span');
    if (!candidate) return false;

    if (candidate.closest('.product-review-item,.review-content,.product-reviews-page,.hood-review-zoom-v107,.hood-review-zoom-overlay')) {
      return false;
    }

    var txt = (candidate.textContent || '').replace(/\s+/g, ' ').trim().toLowerCase();
    if (txt !== 'chat' && txt !== 'open chat' && txt !== 'live chat') return false;

    var cs = window.getComputedStyle(candidate);
    var r = candidate.getBoundingClientRect();
    var isFloating = (
      cs.position === 'fixed' ||
      cs.position === 'sticky' ||
      cs.position === 'absolute' ||
      r.right > window.innerWidth - 180 ||
      r.bottom > window.innerHeight - 180
    );

    return isFloating;
  }

  function clickHandler(ev) {
    var target = ev.target;

    if (!isOurChatButton(target)) return;

    ev.preventDefault();
    ev.stopPropagation();

    openTawk('chat-button-click');
    return false;
  }

  function patchExistingHoodTp() {
    if (!window.HOOD_TP) return;

    /*
      Replace old lazy Tawk opener to prevent duplicate embed.tawk.to scripts.
      Do NOT call the old function; duplicate loads caused Tawk internal $el errors.
    */
    window.HOOD_TP.openTawk = function () {
      return openTawk('HOOD_TP.openTawk');
    };

    window.HOOD_TP.loadTawk = function () {
      return openTawk('HOOD_TP.loadTawk');
    };
  }

  function init() {
    refreshConfig();
    installTelemetryGuard();
    installOnLoadHook();
    removeDuplicateTawkScripts();
    patchExistingHoodTp();

    document.addEventListener('click', clickHandler, true);

    /*
      Do not add a broad touchend handler. On mobile it can double-fire with click
      and cause duplicate Tawk mounting.
    */
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    window.setTimeout(init, 0);
  }

  window.addEventListener('load', function () {
    patchExistingHoodTp();
    removeDuplicateTawkScripts();
  });

  window.HOOD_V1081_openTawk = function () {
    return openTawk('manual-console');
  };

  window.HOOD_V1081_report = function () {
    var scripts = Array.prototype.slice.call(document.querySelectorAll('script[src*="embed.tawk.to"]')).map(function (s) { return s.src; });
    var iframes = Array.prototype.slice.call(document.querySelectorAll('iframe')).filter(function (f) {
      var t = ((f.src || '') + ' ' + (f.title || '') + ' ' + (f.id || '') + ' ' + (f.className || '')).toLowerCase();
      return t.indexOf('tawk') >= 0 || t.indexOf('chat') >= 0;
    }).map(function (f) {
      var r = f.getBoundingClientRect();
      return { src: f.src, title: f.title, id: f.id, w: Math.round(r.width), h: Math.round(r.height), display: getComputedStyle(f).display };
    });

    var data = {
      version: window.HOOD_V1081_TAWK_STABLE_FIX_VERSION,
      oldV108Loaded: !!window.HOOD_V108_TAWK_CSP_FIX_VERSION,
      hasTawkApi: !!window.Tawk_API,
      tawkReady: isTawkReady(),
      loading: state.loading,
      loaded: state.loaded,
      pendingOpen: state.pendingOpen,
      lastOpenReason: state.lastOpenReason,
      duplicateScriptsRemoved: state.duplicateScriptsRemoved,
      telemetryGuard: !!window.HOOD_V1081_TAWK_TELEMETRY_GUARD,
      scripts: scripts,
      iframes: iframes
    };

    console.table(iframes);
    return data;
  };
})();
