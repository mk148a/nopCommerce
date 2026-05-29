(function () {
  'use strict';


  function killLegacyHoodReviewZoom() {
    var selectors = [
      '.hood-review-zoom-v107',
      '.hood-review-zoom-v107-backdrop',
      '.hood-review-zoom-overlay',
      '.hood-review-zoom-backdrop',
      '.hood-review-zoom'
    ];

    var removed = 0;
    selectors.forEach(function (sel) {
      document.querySelectorAll(sel).forEach(function (node) {
        try {
          node.parentNode && node.parentNode.removeChild(node);
          removed++;
        } catch (e) {
          try {
            node.style.setProperty('display', 'none', 'important');
            node.style.setProperty('visibility', 'hidden', 'important');
            node.style.setProperty('left', '-99999px', 'important');
            node.style.setProperty('top', '-99999px', 'important');
          } catch (_) {}
        }
      });
    });

    window.CPR_LEGACY_HOOD_REVIEW_ZOOM_REMOVED = (window.CPR_LEGACY_HOOD_REVIEW_ZOOM_REMOVED || 0) + removed;
    return removed;
  }

  function installLegacyZoomKiller() {
    killLegacyHoodReviewZoom();

    if (window.CPR_LEGACY_HOOD_REVIEW_ZOOM_KILLER_INSTALLED) return;
    window.CPR_LEGACY_HOOD_REVIEW_ZOOM_KILLER_INSTALLED = true;

    try {
      var mo = new MutationObserver(function (mutations) {
        var shouldKill = false;
        mutations.forEach(function (m) {
          (m.addedNodes || []).forEach(function (n) {
            if (!n || n.nodeType !== 1) return;
            var cls = (n.className || '').toString();
            if (cls.indexOf('hood-review-zoom') >= 0 || (n.querySelector && n.querySelector('[class*="hood-review-zoom"]'))) {
              shouldKill = true;
            }
          });
        });
        if (shouldKill) killLegacyHoodReviewZoom();
      });
      mo.observe(document.documentElement || document.body, { childList: true, subtree: true });
      window.CPR_LEGACY_HOOD_REVIEW_ZOOM_OBSERVER = mo;
    } catch (e) {}

    setTimeout(killLegacyHoodReviewZoom, 250);
    setTimeout(killLegacyHoodReviewZoom, 1000);
    setTimeout(killLegacyHoodReviewZoom, 2500);
  }

  window.CUSTOM_PRODUCT_REVIEWS_MEDIA_VERSION = '1.08.2-hood';

  var overlay, backdrop, overlayImg, caption, closeBtn, activeTarget, hideTimer;

  function closest(el, selector) {
    return el && el.closest ? el.closest(selector) : null;
  }

  function containsBadResourceKey(text) {
    text = String(text || '').toLowerCase();
    return text.indexOf('plugins.widgets.customproductreviews') >= 0 || text.indexOf('productreviewsfor') >= 0;
  }

  function cleanText(text, fallback) {
    text = String(text || '').trim();
    if (!text || containsBadResourceKey(text)) return fallback || 'Review image';
    return text;
  }

  function ensureOverlay() {
    if (overlay) return;

    backdrop = document.createElement('div');
    backdrop.className = 'cpr-review-zoom-backdrop';

    overlay = document.createElement('div');
    overlay.className = 'cpr-review-zoom-overlay';
    overlay.setAttribute('role', 'dialog');
    overlay.setAttribute('aria-label', 'Review image preview');

    closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'cpr-review-zoom-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.setAttribute('aria-label', 'Close image preview');

    overlayImg = document.createElement('img');
    overlayImg.alt = '';

    caption = document.createElement('span');
    caption.className = 'cpr-review-zoom-caption';

    overlay.appendChild(closeBtn);
    overlay.appendChild(overlayImg);
    overlay.appendChild(caption);
    document.body.appendChild(backdrop);
    document.body.appendChild(overlay);

    closeBtn.addEventListener('click', function (e) {
      e.preventDefault();
      e.stopPropagation();
      hide(true);
    }, true);

    backdrop.addEventListener('click', function (e) {
      e.preventDefault();
      e.stopPropagation();
      hide(true);
    }, true);
  }

  function isBadUiImage(img) {
    if (!img || img.nodeType !== 1) return true;
    var src = (img.getAttribute('src') || '').toLowerCase();
    var cls = (img.className || '').toString().toLowerCase();

    if (cls.indexOf('avatar') >= 0 || src.indexOf('default-avatar') >= 0) return true;
    if (cls.indexOf('rating') >= 0 || cls.indexOf('star') >= 0 || cls.indexOf('logo') >= 0 || cls.indexOf('icon') >= 0) return true;
    if (src.indexOf('rating') >= 0 || src.indexOf('star') >= 0 || src.indexOf('logo') >= 0 || src.indexOf('/icons/') >= 0) return true;
    if (closest(img, '.gallery,.product-essential,.cloudzoom-gallery,.header,.footer,.rating,.avatar,.review-info .avatar')) return true;
    return false;
  }

  function getReviewTarget(target) {
    var link = closest(target, 'a.cpr-review-media-link[data-cpr-full]');
    if (link && !closest(link, '.gallery,.product-essential,.cloudzoom-gallery,.header,.footer,.rating')) {
      return {
        element: link,
        img: link.querySelector('img'),
        full: link.getAttribute('data-cpr-full') || link.getAttribute('href') || '',
        caption: cleanText(link.getAttribute('data-cpr-title') || link.getAttribute('aria-label') || (link.querySelector('img') && (link.querySelector('img').getAttribute('title') || link.querySelector('img').getAttribute('alt'))), 'Review image')
      };
    }

    var thumb = closest(target, '.product-review-item .mediacontainer .thumb-item, .product-reviews-page .mediacontainer .thumb-item, .review-content .mediacontainer .thumb-item');
    if (thumb) {
      var img = thumb.querySelector('img[src*="/images/thumbs/"]');
      if (!img || isBadUiImage(img)) return null;

      var hidden = thumb.querySelector('.thumb-item-content img[src]');
      var full = img.getAttribute('data-fullsize') || (hidden && hidden.getAttribute('src')) || img.getAttribute('data-defaultsize') || img.currentSrc || img.src;
      if (!full) return null;

      return {
        element: thumb,
        img: img,
        full: full,
        caption: cleanText(img.getAttribute('title') || img.getAttribute('alt'), 'Review image')
      };
    }

    return null;
  }

  function position(x, y) {
    if (!overlay) return;
    var pad = 14;
    var r = overlay.getBoundingClientRect();
    var w = r.width || 480;
    var h = r.height || 560;
    var left = x + 24;
    var top = y + 18;

    if (left + w + pad > window.innerWidth) left = x - w - 24;
    if (left < pad) left = pad;
    if (top + h + pad > window.innerHeight) top = window.innerHeight - h - pad;
    if (top < pad) top = pad;

    overlay.style.left = Math.round(left) + 'px';
    overlay.style.top = Math.round(top) + 'px';
    overlay.style.removeProperty('transform');
  }

  function show(target, e, pinned) {
    if (!target || !target.full) return;
    ensureOverlay();
    clearTimeout(hideTimer);

    activeTarget = target.element;
    overlayImg.src = target.full;
    overlayImg.alt = target.caption || 'Review image';
    caption.textContent = target.caption || 'Review image';

    overlay.classList.toggle('is-pinned', !!pinned);
    overlay.classList.add('is-visible');
    backdrop.classList.toggle('is-visible', !!pinned);

    if (!pinned) {
      var x = e && typeof e.clientX === 'number' ? e.clientX : target.element.getBoundingClientRect().right;
      var y = e && typeof e.clientY === 'number' ? e.clientY : target.element.getBoundingClientRect().top;
      requestAnimationFrame(function () { position(x, y); });
    }
  }

  function hide(now) {
    if (!overlay) return;
    clearTimeout(hideTimer);
    var doHide = function () {
      overlay.classList.remove('is-visible');
      overlay.classList.remove('is-pinned');
      backdrop.classList.remove('is-visible');
      activeTarget = null;
    };
    if (now) doHide();
    else hideTimer = setTimeout(doHide, 120);
  }

  document.addEventListener('mouseover', function (e) {
    var t = getReviewTarget(e.target);
    if (!t) return;
    show(t, e, false);
  }, true);

  document.addEventListener('mousemove', function (e) {
    if (!overlay || !overlay.classList.contains('is-visible') || overlay.classList.contains('is-pinned')) return;
    var t = getReviewTarget(e.target);
    if (!t || t.element !== activeTarget) return;
    position(e.clientX, e.clientY);
  }, true);

  document.addEventListener('mouseout', function (e) {
    var t = getReviewTarget(e.target);
    if (!t || t.element !== activeTarget) return;
    if (e.relatedTarget && t.element.contains(e.relatedTarget)) return;
    hide(false);
  }, true);

  document.addEventListener('click', function (e) {
    var t = getReviewTarget(e.target);
    if (!t) return;

    if (window.matchMedia('(max-width: 768px)').matches) {
      e.preventDefault();
      e.stopPropagation();
      show(t, e, true);
    }
  }, true);

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') hide(true);
  }, true);

  window.addEventListener('scroll', function () { hide(true); }, { passive: true });
  window.addEventListener('resize', function () { hide(true); }, { passive: true });

  function markAndClean() {
    installLegacyZoomKiller();
    var count = 0;
    document.querySelectorAll('.product-review-item .mediacontainer img[src*="/images/thumbs/"], .product-reviews-page .mediacontainer img[src*="/images/thumbs/"], a.cpr-review-media-link img').forEach(function (img) {
      if (isBadUiImage(img)) return;
      img.classList.add('cpr-review-image-js-ready');
      img.setAttribute('title', cleanText(img.getAttribute('title'), 'Hover to zoom'));
      count++;
    });

    document.querySelectorAll('.product-review-item .thumb-item-content, .product-reviews-page .thumb-item-content').forEach(function (x) {
      x.style.setProperty('display', 'none', 'important');
      x.style.setProperty('visibility', 'hidden', 'important');
      x.style.setProperty('pointer-events', 'none', 'important');
    });

    window.CPR_LAST_READY_IMAGES = count;
    return count;
  }

  installLegacyZoomKiller();

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', markAndClean, { once: true });
  } else {
    markAndClean();
  }
  window.addEventListener('load', function () {
    setTimeout(markAndClean, 100);
    setTimeout(markAndClean, 1000);
  });

  window.CPR_reviewMediaReport = function () {
    var links = Array.prototype.slice.call(document.querySelectorAll('a.cpr-review-media-link[data-cpr-full]'));
    var legacy = Array.prototype.slice.call(document.querySelectorAll('.product-review-item .mediacontainer .thumb-item img[src*="/images/thumbs/"]')).filter(function (img) { return !isBadUiImage(img); });
    var data = {
      version: window.CUSTOM_PRODUCT_REVIEWS_MEDIA_VERSION,
      links: links.length,
      legacyImages: legacy.length,
      readyImages: window.CPR_LAST_READY_IMAGES || 0,
      legacyHoodZoomRemoved: window.CPR_LEGACY_HOOD_REVIEW_ZOOM_REMOVED || 0,
      legacyHoodZoomNodes: document.querySelectorAll('[class*="hood-review-zoom"]').length,
      items: links.map(function (a) {
        var img = a.querySelector('img');
        var r = img ? img.getBoundingClientRect() : { width: 0, height: 0 };
        return {
          href: (a.href || '').slice(0, 100),
          full: (a.getAttribute('data-cpr-full') || '').slice(0, 100),
          imgW: Math.round(r.width || 0),
          imgH: Math.round(r.height || 0),
          alt: img ? img.alt : ''
        };
      })
    };
    console.table(data.items);
    return data;
  };
})();
