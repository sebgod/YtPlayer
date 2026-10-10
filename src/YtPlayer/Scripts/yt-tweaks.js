// Injected by YtPlayer at the start of every YouTube document, before YouTube's own scripts.
// It hides Shorts, opens a Short as an ordinary video, and implements compact mode (the video
// alone, filling the window). YouTube changes its markup often: when Shorts reappear, the
// selectors in HIDE_SHORTS are the place to look.
(function () {
  if (window.__ytPip) return;
  window.__ytPip = true;
  if (!/(^|\.)youtube\.com$/.test(location.hostname)) return;

  var HIDE_SHORTS = [
    // Shelves on Home, Subscriptions and search.
    'ytd-reel-shelf-renderer',
    'ytd-rich-shelf-renderer[is-shorts]',
    'ytd-rich-section-renderer:has(ytd-rich-shelf-renderer[is-shorts])',
    'grid-shelf-view-model:has(ytm-shorts-lockup-view-model)',
    'grid-shelf-view-model:has(ytm-shorts-lockup-view-model-v2)',
    'ytm-shorts-lockup-view-model',
    'ytm-shorts-lockup-view-model-v2',
    // Single Shorts in a feed, a search result list or a channel grid.
    'ytd-rich-item-renderer:has(a[href^="/shorts/"])',
    'ytd-video-renderer:has(a[href^="/shorts/"])',
    'ytd-grid-video-renderer:has(a[href^="/shorts/"])',
    'ytd-compact-video-renderer:has(a[href^="/shorts/"])',
    // The Shorts entry in the side menus and the Shorts tab on a channel.
    'ytd-guide-entry-renderer:has(a[title="Shorts"])',
    'ytd-mini-guide-entry-renderer[aria-label="Shorts"]',
    'yt-tab-shape[tab-title="Shorts"]',
    'tp-yt-paper-tab:has(> .tab-content[title="Shorts"])'
  ];

  var CSS =
    HIDE_SHORTS.join(',\n') + ' { display: none !important; }\n' +
    // Compact: the player alone, over everything, filling the window. YouTube re-lays its video out
    // from the player's own box, so moving that box is enough; its controls come along.
    'html.ytpip-compact, html.ytpip-compact body { overflow: hidden !important; background: #000 !important; }\n' +
    'html.ytpip-compact #movie_player { position: fixed !important; inset: 0 !important;' +
    ' width: 100vw !important; height: 100vh !important; z-index: 2147483000 !important; background: #000 !important; }\n' +
    'html.ytpip-compact #masthead-container, html.ytpip-compact ytd-mini-guide-renderer,' +
    ' html.ytpip-compact tp-yt-app-drawer { display: none !important; }\n' +
    // End-screen cards and the "more videos" strip cover most of a small window.
    'html.ytpip-compact .ytp-ce-element, html.ytpip-compact .ytp-pause-overlay { display: none !important; }\n';

  function addStyle() {
    var style = document.createElement('style');
    style.id = 'ytpip-style';
    style.textContent = CSS;
    (document.head || document.documentElement).appendChild(style);
  }
  if (document.documentElement) addStyle();
  else document.addEventListener('readystatechange', addStyle, { once: true });

  // A Short opens as an ordinary video: on a full load, on YouTube's in-page navigation, and on a click.
  function shortId(href) {
    var m = /\/shorts\/([A-Za-z0-9_-]{11})/.exec(href || '');
    return m ? m[1] : null;
  }
  function redirectIfShort() {
    var id = shortId(location.pathname);
    if (id) location.replace('/watch?v=' + id);
  }
  redirectIfShort();
  document.addEventListener('yt-navigate-start', function (e) {
    var url = e && e.detail && e.detail.url;
    var id = shortId(url);
    if (id) { e.stopImmediatePropagation(); location.assign('/watch?v=' + id); }
  }, true);
  document.addEventListener('yt-navigate-finish', redirectIfShort);
  document.addEventListener('click', function (e) {
    var a = e.target && e.target.closest ? e.target.closest('a[href*="/shorts/"]') : null;
    var id = a && shortId(a.getAttribute('href'));
    if (!id) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    location.assign('/watch?v=' + id);
  }, true);

  // Host <-> page. The host says whether compact mode is on; the page says when it is ready for that,
  // and reports the video it shows so the host can title the window.
  // Compact is the host's preference, applied only on a watch page. Anywhere else there is no video to
  // fill the window, and hiding the masthead and the scrollbar would only leave the page cut off, so
  // there it waits for the next video (YouTube's in-page navigation re-checks it).
  var compactWanted = false;
  function applyCompact() {
    var root = document.documentElement;
    var on = compactWanted && location.pathname === '/watch';
    if (root.classList.contains('ytpip-compact') === on) return;
    root.classList.toggle('ytpip-compact', on);
    window.dispatchEvent(new Event('resize'));
  }
  function setCompact(on) {
    compactWanted = !!on;
    applyCompact();
  }
  document.addEventListener('yt-navigate-finish', applyCompact);
  function post(msg) {
    try { window.chrome.webview.postMessage(msg); } catch (e) { /* no host bridge */ }
  }
  try {
    window.chrome.webview.addEventListener('message', function (e) {
      var d = e.data;
      if (d && typeof d.compact === 'boolean') setCompact(d.compact);
      if (d && d.togglePlay) {
        var v = document.querySelector('video');
        if (v) { if (v.paused) v.play(); else v.pause(); }
      }
    });
  } catch (e) { /* no host bridge */ }
  document.addEventListener('DOMContentLoaded', function () { post({ ready: true }); });
  document.addEventListener('yt-navigate-finish', function () {
    post({ navigated: location.href, watching: location.pathname === '/watch' });
  });
})();
