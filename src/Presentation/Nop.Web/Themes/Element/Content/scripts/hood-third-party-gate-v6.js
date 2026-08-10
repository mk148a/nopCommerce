/* Hood Archery Shop - Third Party Gate V6.6
   Purpose:
   - Get a real visitor country code even when Cloudflare CF-IPCountry is not available in the browser.
   - Prefer server-provided country if available.
   - Fallback to api.country.is with timeout + localStorage/cache.
   - Do NOT use browser language/timezone inference by default because it can create false positives.
   - Keep Facebook disabled by default if nopCommerce Facebook Pixel plugin is enabled.
*/
(function (window, document) {
    'use strict';

    var defaultConfig = {
        debug: false,
        version: '6.6.0',

        loadGtm: false,
        gtmId: '',
        gtmDelayMs: 1500,

        loadFacebook: false,
        facebookPixelId: '',
        facebookDelayMs: 4500,

        loadPinterest: true,
        pinterestTagId: '',
        pinterestDelayMs: 5500,

        loadTrustedSite: true,
        trustedSiteSrc: 'https://cdn.ywxi.net/js/2.js',
        trustedSiteDelayMs: 6500,

        loadTawkOnClick: true,
        tawkSrc: '',

        scriptTimeoutMs: 3000,
        skipMarketingOnSlowConnection: true,

        // Country logic
        allowMarketingWhenCountryUnknown: true,
        blockMarketingWhenCountryUnknown: false, // legacy alias, keep false
        useRemoteCountryLookup: true,
        countryLookupUrl: 'https://api.country.is/',
        countryLookupTimeoutMs: 1500,
        countryCacheHours: 24,
        countryGateMaxWaitMs: 1800,

        // Disabled by default: timezone/language gave false BY/RU results on your machine.
        inferBlockedCountryFromBrowser: false,

        blockedMarketingCountries: [
            'RU','BY','UA','CN','IR','KP','MM','TM','UG','SY','CU','SD','SS','AF','YE','LY','SO','VE','XX','T1'
        ]
    };

    var cfg = merge(defaultConfig, window.HOOD_TP_CONFIG || {});
    cfg.blockedMarketingCountries = normalizeCountryList(cfg.blockedMarketingCountries);

    var state = {
        gtm: false,
        facebook: false,
        pinterest: false,
        trustedSite: false,
        tawk: false,
        skippedReason: '',
        detectedCountry: '',
        countrySource: '',
        countryLookupStatus: 'not-started',
        countryLookupError: '',
        inferredBlockedCountry: '',
        countryLookupPromise: null,
        facebookSource: ''
    };

    function merge(a, b) {
        var out = {};
        Object.keys(a || {}).forEach(function (k) { out[k] = a[k]; });
        Object.keys(b || {}).forEach(function (k) { out[k] = b[k]; });
        return out;
    }

    function normalizeCountryCode(v) {
        if (!v) return '';
        v = String(v).trim().toUpperCase();
        if (v === 'UK') return 'GB';
        if (v === 'UNKNOWN') return 'UNKNOWN';
        if (!/^[A-Z]{2}$/.test(v)) return '';
        return v;
    }

    function normalizeCountryList(list) {
        return (list || []).map(normalizeCountryCode).filter(Boolean);
    }

    function log() {
        if (!cfg.debug || !window.console) return;
        console.log.apply(console, ['[HOOD_TP_V6.6]'].concat([].slice.call(arguments)));
    }

    function getStorage(key) {
        try { return window.localStorage && window.localStorage.getItem(key); } catch (e) { return null; }
    }

    function setStorage(key, value) {
        try { if (window.localStorage) window.localStorage.setItem(key, value); } catch (e) { }
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name.replace(/([.$?*|{}()\[\]\\/+^])/g, '\\$1') + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(name, value, maxAgeSeconds) {
        try {
            document.cookie = name + '=' + encodeURIComponent(value) + '; path=/; max-age=' + maxAgeSeconds + '; SameSite=Lax';
        } catch (e) { }
    }

    function cacheCountry(country, source) {
        country = normalizeCountryCode(country);
        if (!country || country === 'UNKNOWN') return;
        var data = JSON.stringify({ country: country, source: source || 'cache', ts: Date.now() });
        setStorage('HOOD_TP_COUNTRY_V1', data);
        setCookie('HOOD_TP_COUNTRY', country, Math.max(3600, (cfg.countryCacheHours || 24) * 3600));
    }

    function readCachedCountry() {
        var raw = getStorage('HOOD_TP_COUNTRY_V1');
        if (raw) {
            try {
                var data = JSON.parse(raw);
                var ageMs = Date.now() - (data.ts || 0);
                var maxAgeMs = Math.max(1, cfg.countryCacheHours || 24) * 3600 * 1000;
                var c = normalizeCountryCode(data.country);
                if (c && c !== 'UNKNOWN' && ageMs >= 0 && ageMs <= maxAgeMs) {
                    return { country: c, source: 'localStorage' };
                }
            } catch (e) { }
        }

        var cookieCountry = normalizeCountryCode(getCookie('HOOD_TP_COUNTRY'));
        if (cookieCountry && cookieCountry !== 'UNKNOWN') {
            return { country: cookieCountry, source: 'cookie' };
        }

        return null;
    }

    function setCountry(country, source) {
        country = normalizeCountryCode(country);
        if (!country) return '';
        state.detectedCountry = country;
        state.countrySource = source || '';
        if (country !== 'UNKNOWN') cacheCountry(country, source);
        return country;
    }

    function getCountrySync() {
        if (state.detectedCountry) return state.detectedCountry;

        var candidates = [
            cfg.country,
            window.HOOD_COUNTRY,
            window.HOOD_COUNTRY_CODE,
            document.documentElement && document.documentElement.getAttribute('data-country'),
            document.body && document.body.getAttribute('data-country')
        ];

        for (var i = 0; i < candidates.length; i++) {
            var c = normalizeCountryCode(candidates[i]);
            if (c && c !== 'UNKNOWN') return setCountry(c, 'server-or-config');
        }

        var cached = readCachedCountry();
        if (cached) return setCountry(cached.country, cached.source);

        return setCountry('UNKNOWN', 'unknown');
    }

    function fetchJsonWithTimeout(url, timeoutMs) {
        var controller = window.AbortController ? new AbortController() : null;
        var timer = null;
        var opts = { method: 'GET', cache: 'no-store', credentials: 'omit', mode: 'cors' };
        if (controller) {
            opts.signal = controller.signal;
            timer = window.setTimeout(function () { try { controller.abort(); } catch (e) { } }, timeoutMs || 1500);
        }

        return window.fetch(url, opts).then(function (res) {
            if (timer) window.clearTimeout(timer);
            if (!res.ok) throw new Error('country lookup HTTP ' + res.status);
            return res.json();
        }).catch(function (err) {
            if (timer) window.clearTimeout(timer);
            throw err;
        });
    }

    function resolveCountry() {
        var current = getCountrySync();
        if (current && current !== 'UNKNOWN') {
            state.countryLookupStatus = 'ready';
            return Promise.resolve(current);
        }

        if (!cfg.useRemoteCountryLookup || !cfg.countryLookupUrl || !window.fetch) {
            state.countryLookupStatus = 'unavailable';
            return Promise.resolve('UNKNOWN');
        }

        if (state.countryLookupPromise) return state.countryLookupPromise;

        state.countryLookupStatus = 'pending';
        state.countryLookupError = '';

        state.countryLookupPromise = fetchJsonWithTimeout(cfg.countryLookupUrl, cfg.countryLookupTimeoutMs || 1500)
            .then(function (data) {
                var country = normalizeCountryCode(data && (data.country || data.countryCode || data.country_code));
                if (country && country !== 'UNKNOWN') {
                    setCountry(country, 'remote:' + cfg.countryLookupUrl);
                    state.countryLookupStatus = 'ready';
                    log('Country resolved', country);
                    return country;
                }
                state.countryLookupStatus = 'empty';
                return 'UNKNOWN';
            })
            .catch(function (err) {
                state.countryLookupStatus = 'failed';
                state.countryLookupError = err && err.message ? err.message : String(err || 'country lookup failed');
                log('Country lookup failed', state.countryLookupError);
                return 'UNKNOWN';
            });

        return state.countryLookupPromise;
    }

    function isSlowConnection() {
        var c = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        if (!c) return false;
        if (c.saveData) return true;
        var type = (c.effectiveType || '').toLowerCase();
        return type === 'slow-2g' || type === '2g';
    }

    function inferBlockedCountryFromBrowser() {
        // Off by default. Only enable manually if you accept false-positive risk.
        if (!cfg.inferBlockedCountryFromBrowser) return '';

        var tz = '';
        try { tz = Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) { }

        var map = {
            'Europe/Moscow': 'RU', 'Europe/Kaliningrad': 'RU', 'Europe/Samara': 'RU',
            'Asia/Yekaterinburg': 'RU', 'Asia/Omsk': 'RU', 'Asia/Novosibirsk': 'RU',
            'Asia/Krasnoyarsk': 'RU', 'Asia/Irkutsk': 'RU', 'Asia/Yakutsk': 'RU',
            'Asia/Vladivostok': 'RU', 'Asia/Magadan': 'RU', 'Asia/Kamchatka': 'RU',
            'Europe/Minsk': 'BY', 'Europe/Kyiv': 'UA', 'Europe/Kiev': 'UA',
            'Asia/Shanghai': 'CN', 'Asia/Urumqi': 'CN', 'Asia/Tehran': 'IR',
            'Asia/Pyongyang': 'KP', 'Asia/Yangon': 'MM', 'Asia/Ashgabat': 'TM',
            'Asia/Damascus': 'SY', 'America/Havana': 'CU', 'Africa/Khartoum': 'SD',
            'Africa/Juba': 'SS', 'Asia/Kabul': 'AF', 'Asia/Aden': 'YE',
            'Africa/Tripoli': 'LY', 'Africa/Mogadishu': 'SO', 'America/Caracas': 'VE'
        };
        return map[tz] || '';
    }

    function marketingAllowed() {
        var country = getCountrySync();

        if (cfg.skipMarketingOnSlowConnection && isSlowConnection()) {
            state.skippedReason = 'slow-connection-or-save-data';
            return false;
        }

        if (country !== 'UNKNOWN' && cfg.blockedMarketingCountries.indexOf(country) >= 0) {
            state.skippedReason = 'blocked-country-' + country;
            return false;
        }

        if (country === 'UNKNOWN') {
            var inferred = inferBlockedCountryFromBrowser();
            state.inferredBlockedCountry = inferred || '';
            if (inferred && cfg.blockedMarketingCountries.indexOf(inferred) >= 0) {
                state.skippedReason = 'inferred-blocked-country-' + inferred;
                return false;
            }
            if (cfg.blockMarketingWhenCountryUnknown || cfg.allowMarketingWhenCountryUnknown === false) {
                state.skippedReason = 'country-unknown';
                return false;
            }
        }

        state.skippedReason = '';
        return true;
    }

    function afterCountryGate(fn) {
        var done = false;
        function run() {
            if (done) return;
            done = true;
            try { fn(); } catch (e) { log('loader error', e); }
        }
        var maxWait = cfg.countryGateMaxWaitMs || 1800;
        var timer = window.setTimeout(run, maxWait);
        resolveCountry().then(function () {
            window.clearTimeout(timer);
            run();
        }).catch(function () {
            window.clearTimeout(timer);
            run();
        });
    }

    function loadScriptOnce(id, src, timeoutMs, beforeAppend) {
        return new Promise(function (resolve, reject) {
            if (!src) return reject(new Error('Missing script src: ' + id));
            if (document.getElementById(id)) return resolve(document.getElementById(id));

            var done = false;
            var s = document.createElement('script');
            s.id = id;
            s.async = true;
            s.src = src;

            var timer = window.setTimeout(function () {
                if (done) return;
                done = true;
                try { s.remove(); } catch (e) { }
                reject(new Error('Script timeout: ' + src));
            }, timeoutMs || cfg.scriptTimeoutMs || 3000);

            s.onload = function () {
                if (done) return;
                done = true;
                window.clearTimeout(timer);
                resolve(s);
            };
            s.onerror = function () {
                if (done) return;
                done = true;
                window.clearTimeout(timer);
                reject(new Error('Script failed: ' + src));
            };

            if (typeof beforeAppend === 'function') beforeAppend(s);
            (document.head || document.documentElement).appendChild(s);
        });
    }

    function loadGtm() {
        if (!cfg.loadGtm || !cfg.gtmId || state.gtm || !marketingAllowed()) return;
        window.dataLayer = window.dataLayer || [];
        window.dataLayer.push({ 'gtm.start': new Date().getTime(), event: 'gtm.js' });
        loadScriptOnce('hood-gtm-js', 'https://www.googletagmanager.com/gtm.js?id=' + encodeURIComponent(cfg.gtmId), cfg.scriptTimeoutMs)
            .then(function () { state.gtm = true; log('GTM loaded'); })
            .catch(function (e) { log(e.message); });
    }


    function detectExternalFacebook(source) {
        try {
            if (window.fbq && window.fbq.loaded) {
                state.facebook = true;
                state.facebookSource = source || state.facebookSource || 'external-plugin-or-gtm';
                return true;
            }
        } catch (e) { }
        return false;
    }

    function facebookStatus() {
        var foundScript = false;
        try {
            foundScript = !!document.querySelector('script[src*="connect.facebook.net"][src*="fbevents.js"]');
        } catch (e) { }
        return {
            state: !!state.facebook,
            source: state.facebookSource || '',
            fbqLoaded: !!(window.fbq && window.fbq.loaded),
            fbeventsScript: foundScript,
            configuredToLoad: !!cfg.loadFacebook,
            pixelId: cfg.facebookPixelId || '',
            country: getCountrySync(),
            marketingAllowed: marketingAllowed()
        };
    }

    function loadFacebook() {
        if (!cfg.loadFacebook || !cfg.facebookPixelId || state.facebook || !marketingAllowed()) return;
        if (detectExternalFacebook('external-plugin-or-gtm')) {
            log('Facebook already loaded by another plugin or GTM');
            return;
        }
        !function(f,b,e,v,n,t,s){
            if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};
            if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];
            t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s);
        }(window, document, 'script', 'https://connect.facebook.net/en_US/fbevents.js');
        try { window.fbq('init', cfg.facebookPixelId); window.fbq('track', 'PageView'); state.facebook = true; state.facebookSource = 'v6-loader'; log('Facebook Pixel loaded'); }
        catch (e) { log('Facebook Pixel error', e); }
    }

    function loadPinterest() {
        if (!cfg.loadPinterest || !cfg.pinterestTagId || state.pinterest || !marketingAllowed()) return;
        !function(e){
            if(!window.pintrk){
                window.pintrk = function () { window.pintrk.queue.push(Array.prototype.slice.call(arguments)); };
                var n = window.pintrk; n.queue = []; n.version = '3.0';
                var t = document.createElement('script'); t.async = true; t.src = e; t.id = 'hood-pinterest-core';
                var r = document.getElementsByTagName('script')[0]; r.parentNode.insertBefore(t, r);
            }
        }('https://s.pinimg.com/ct/core.js');
        try { window.pintrk('load', cfg.pinterestTagId); window.pintrk('page'); state.pinterest = true; log('Pinterest loaded'); }
        catch (e) { log('Pinterest error', e); }
    }

    function loadTrustedSite() {
        if (!cfg.loadTrustedSite || !cfg.trustedSiteSrc || state.trustedSite || !marketingAllowed()) return;
        loadScriptOnce('hood-trustedsite-js', cfg.trustedSiteSrc, cfg.scriptTimeoutMs)
            .then(function () { state.trustedSite = true; log('TrustedSite loaded'); })
            .catch(function (e) { log(e.message); });
    }

    function loadTawk() {
        if (!cfg.loadTawkOnClick || !cfg.tawkSrc || state.tawk) return;
        window.Tawk_API = window.Tawk_API || {};
        window.Tawk_LoadStart = new Date();
        loadScriptOnce('hood-tawk-js', cfg.tawkSrc, cfg.scriptTimeoutMs, function (s) {
            s.charset = 'UTF-8';
            s.setAttribute('crossorigin', '*');
        }).then(function () { state.tawk = true; log('Tawk loaded'); })
          .catch(function (e) { log(e.message); });
    }

    function createChatButton() {
        if (!cfg.loadTawkOnClick || !cfg.tawkSrc) return;
        if (document.getElementById('hood-chat-button')) return;
        var btn = document.createElement('button');
        btn.id = 'hood-chat-button';
        btn.type = 'button';
        btn.className = 'hood-chat-button';
        btn.setAttribute('aria-label', 'Open chat');
        btn.innerHTML = '<span class="hood-chat-dot"></span><span class="hood-chat-text">Chat</span>';
        btn.addEventListener('click', function () { loadTawk(); btn.classList.add('hood-chat-loading'); }, { passive: true });
        document.body.appendChild(btn);
    }

    function schedule() {
        getCountrySync();
        resolveCountry();
        detectExternalFacebook('external-plugin-or-gtm');
        var fbDetectCount = 0;
        var fbDetectTimer = window.setInterval(function () {
            fbDetectCount++;
            if (detectExternalFacebook('external-plugin-or-gtm') || fbDetectCount >= 8) {
                window.clearInterval(fbDetectTimer);
            }
        }, 1500);
        window.setTimeout(function () { afterCountryGate(loadGtm); }, cfg.gtmDelayMs || 1500);
        window.setTimeout(function () { afterCountryGate(loadFacebook); }, cfg.facebookDelayMs || 4500);
        window.setTimeout(function () { afterCountryGate(loadPinterest); }, cfg.pinterestDelayMs || 5500);
        window.setTimeout(function () { afterCountryGate(loadTrustedSite); }, cfg.trustedSiteDelayMs || 6500);
        createChatButton();
    }

    window.HOOD_TP = {
        config: cfg,
        state: state,
        country: getCountrySync,
        resolveCountry: resolveCountry,
        marketingAllowed: marketingAllowed,
        loadTawk: loadTawk,
        loadPinterest: loadPinterest,
        loadTrustedSite: loadTrustedSite,
        loadFacebook: loadFacebook,
        facebookStatus: facebookStatus,
        version: cfg.version
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', schedule, { once: true });
    } else {
        schedule();
    }
})(window, document);


