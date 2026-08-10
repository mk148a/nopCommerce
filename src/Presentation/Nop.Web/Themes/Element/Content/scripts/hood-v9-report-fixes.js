(function(){
'use strict';

window.HOOD_V9_REPORT_FIX_VERSION='9.3.0';

function all(s,r){return Array.prototype.slice.call((r||document).querySelectorAll(s));}
function ytId(src){if(!src)return'';try{var u=new URL(src,location.href);var m=u.pathname.match(/\/embed\/([^\/\?&#]+)/i);return m?decodeURIComponent(m[1]):'';}catch(e){var m2=String(src).match(/\/embed\/([^\/\?&#"']+)/i);return m2?m2[1]:'';}}
function norm(src){var id=ytId(src);return id?'https://www.youtube-nocookie.com/embed/'+encodeURIComponent(id):src;}
function poster(src){var id=ytId(src);return id?'https://i.ytimg.com/vi/'+encodeURIComponent(id)+'/hqdefault.jpg':'';}

function enhanceLite(el){
  if(!el || el.nodeType!==1) return;
  var src=el.getAttribute('data-hood-youtube-src')||'';
  if(src && !el.style.getPropertyValue('--hood-youtube-poster')){
    var p=poster(src);
    if(p) el.style.setProperty('--hood-youtube-poster','url("'+p.replace(/"/g,'%22')+'")');
  }
  if(el.getAttribute('data-hood-youtube-bound')==='true') return;
  el.setAttribute('data-hood-youtube-bound','true');
  el.addEventListener('click',function(){activate(el);});
}

function makeLite(src,title){
  var n=norm(src),d=document.createElement('div');
  d.className='hood-youtube-lite';
  d.setAttribute('data-hood-youtube-src',n);
  d.setAttribute('data-hood-youtube-upgraded','true');
  var p=poster(n);
  if(p)d.style.setProperty('--hood-youtube-poster','url("'+p.replace(/"/g,'%22')+'")');
  var b=document.createElement('button');
  b.type='button'; b.className='hood-youtube-play'; b.setAttribute('aria-label',title||'Play YouTube video');
  var i=document.createElement('span'); i.className='hood-youtube-play-icon'; i.setAttribute('aria-hidden','true');
  var t=document.createElement('span'); t.className='hood-youtube-title'; t.textContent=title||'Play video';
  b.appendChild(i); b.appendChild(t); d.appendChild(b);
  enhanceLite(d);
  return d;
}

function activate(c){
  if(!c||c.getAttribute('data-hood-youtube-loaded')==='true')return;
  var src=c.getAttribute('data-hood-youtube-src');
  if(!src)return;
  var f=document.createElement('iframe');
  f.src=src+(src.indexOf('?')>=0?'&':'?')+'autoplay=1&rel=0';
  f.width='560'; f.height='315'; f.loading='lazy';
  f.title=c.getAttribute('data-hood-youtube-title')||'YouTube video';
  f.allow='accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share';
  f.allowFullscreen=true; f.setAttribute('frameborder','0');
  f.style.cssText='width:100%;height:100%;position:absolute;inset:0';
  c.setAttribute('data-hood-youtube-loaded','true');
  c.innerHTML=''; c.appendChild(f);
}

function bindLite(){
  all('.hood-youtube-lite[data-hood-youtube-src]').forEach(enhanceLite);
}

function fallbackIframes(){
  all('iframe[src*="youtube.com/embed"],iframe[src*="youtube-nocookie.com/embed"]').forEach(function(f){
    if(f.closest('.hood-youtube-lite'))return;
    f.parentNode.replaceChild(makeLite(f.getAttribute('src')||'',f.getAttribute('title')||'Play YouTube video'),f);
  });
  bindLite();
}

function cloudzoom(){
  all('a.cloudzoom-gallery,a.cloudzoom-gallery-img,a.thumb-item').forEach(function(a,idx){
    var img=a.querySelector('img');
    var full=a.getAttribute('href')||a.getAttribute('data-full-image-url')||a.getAttribute('data-fullimage')||a.getAttribute('data-defaultsize')||a.getAttribute('data-fullsize')||(img&&(img.getAttribute('data-fullsize')||img.getAttribute('data-defaultsize')||img.getAttribute('src')))||'';
    if(!a.getAttribute('href')&&full)a.setAttribute('href',full);
    if(!a.getAttribute('aria-label'))a.setAttribute('aria-label',(img&&img.getAttribute('alt'))||('View product image '+(idx+1)));
    if(a.dataset.hoodV9Bound!=='1'){
      a.dataset.hoodV9Bound='1';
      a.addEventListener('click',function(ev){if(!ev.defaultPrevented)ev.preventDefault();},true);
    }
  });
}

function labels(){
  all('.product-details-page .attributes select,.product-details-page .attributes input,.product-details-page .attributes textarea').forEach(function(el){
    if(el.getAttribute('aria-label')||el.getAttribute('aria-labelledby'))return;
    var txt='',dd=el.closest('dd');
    if(dd){
      var p=dd.previousElementSibling;
      while(p&&p.tagName&&p.tagName.toLowerCase()!=='dt')p=p.previousElementSibling;
      if(p)txt=(p.textContent||'').replace(/\s+/g,' ').trim();
    }
    if(!txt)txt=el.getAttribute('name')||'Product option';
    el.setAttribute('aria-label',txt.replace(/\*+$/,'').trim());
  });

  [
    ['a[href*="twitter"],a[href*="x.com"]','Share on X / Twitter'],
    ['a[href*="facebook"]','Share on Facebook'],
    ['a[href*="pinterest"]','Share on Pinterest'],
    ['.email-a-friend a,a[href*="emailafriend"]','Email this product to a friend'],
    ['.compare-products a,a[href*="compareproducts"]','Add this product to compare list']
  ].forEach(function(p){
    all(p[0]).forEach(function(a){
      if(!a.getAttribute('aria-label'))a.setAttribute('aria-label',p[1]);
      if(!a.getAttribute('title'))a.setAttribute('title',p[1]);
    });
  });
}

function canonical(){
  var cs=all('link[rel="canonical" i]'),wanted=location.origin+location.pathname;
  if(wanted.slice(-1)==='/'&&location.pathname!=='/')wanted=wanted.slice(0,-1);
  var f=cs[0];
  if(!f){f=document.createElement('link');f.rel='canonical';document.head.appendChild(f);}
  f.href=wanted;
  for(var i=1;i<cs.length;i++)cs[i].remove();
}

function run(){bindLite();cloudzoom();labels();canonical();}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',run,{once:true});else run();
window.addEventListener('load',function(){setTimeout(function(){fallbackIframes();run();},250);setTimeout(run,1500);});

window.HOOD_V9_report=function(){
  var d={
    version:window.HOOD_V9_REPORT_FIX_VERSION,
    loadedScript:!!document.querySelector('script[src*="hood-v9-report-fixes"]'),
    loadedCss:!!document.querySelector('link[href*="hood-v9-report-fixes"]'),
    youtubeIframes:all('iframe[src*="youtube"]').length,
    youtubeLite:all('.hood-youtube-lite').length,
    cloudZoomWithoutHref:all('a.cloudzoom-gallery:not([href]),a.thumb-item:not([href])').length,
    productAttrsWithoutLabel:all('.product-details-page .attributes select:not([aria-label]):not([aria-labelledby]),.product-details-page .attributes input:not([aria-label]):not([aria-labelledby])').length,
    canonicals:all('link[rel="canonical" i]').map(function(x){return x.href;})
  };
  console.table([d]);
  return d;
};
})();

