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
    'html.ytpip-compact .ytp-ce-element, html.ytpip-compact .ytp-pause-overlay { display: none !important; }\n' +
    // YouTube's own miniplayer, which takes over when you leave a watch page, is a picture-in-picture
    // inside ours and covers most of a small window. Hidden, the video keeps playing (media keys still
    // reach it); the compact button brings it back.
    'ytd-miniplayer { display: none !important; }\n';

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

  // An ad: once YouTube offers its Skip button, press it, the click the viewer would make. Nothing is
  // blocked; an ad that cannot be skipped, and the seconds before Skip appears, play as usual.
  // The newer ad overlay holds its Skip button (or, for an ad that cannot be skipped, the countdown to
  // the video) in a skip-or-preview container; the older class names are kept for the older layout.
  var SKIP_BUTTON = '.ytp-skip-ad-button, .ytp-ad-skip-button, .ytp-ad-skip-button-modern,' +
    ' .ytp-ad-player-overlay-layout__skip-or-preview-container button[class*="skip"]';
  setInterval(function () {
    if (!document.querySelector('#movie_player.ad-showing')) return;
    var skip = document.querySelector(SKIP_BUTTON);
    if (skip && skip.offsetParent !== null && !skip.disabled) skip.click();
  }, 500);

  // Host <-> page. The host says whether compact mode is on; the page says when it is ready for that,
  // and reports the video it shows so the host can title the window.
  // Compact is the host's preference, applied only on a watch page. Anywhere else there is no video to
  // fill the window, and hiding the masthead and the scrollbar would only leave the page cut off, so
  // there it waits for the next video (YouTube's in-page navigation re-checks it).
  function isWatchPage() { return location.pathname === '/watch'; }
  var compactWanted = false;
  // Set while the search box is open from Ctrl+F. Compact hides the masthead the box lives in, so it
  // steps aside until the next navigation (a search leaves the watch page anyway) or Escape.
  var searching = false;
  // Tells the host whether compact is on screen whenever that changes (the window takes its small
  // size while it is), and when asked to: in answer to the host's compact state, after every full load.
  function applyCompact(report) {
    var root = document.documentElement;
    var on = compactWanted && !searching && isWatchPage();
    var changed = root.classList.contains('ytpip-compact') !== on;
    if (changed) {
      root.classList.toggle('ytpip-compact', on);
      window.dispatchEvent(new Event('resize'));
    }
    if (changed || report) post({ showing: on });
  }
  // A video playing on in the hidden miniplayer: YouTube moves its player there on leaving a watch page.
  function miniplayerActive() {
    return !isWatchPage() &&
      !!document.querySelector('ytd-app[miniplayer-is-active], ytd-miniplayer #movie_player, ytd-miniplayer video');
  }
  function pressKey(key, code, keyCode) {
    var init = { key: key, code: code, keyCode: keyCode, which: keyCode, bubbles: true, cancelable: true };
    document.dispatchEvent(new KeyboardEvent('keydown', init));
    document.dispatchEvent(new KeyboardEvent('keyup', init));
  }
  function setCompact(on) {
    var turnedOn = !!on && !compactWanted;
    compactWanted = !!on;
    // Turned on away from a video while one plays on in the hidden miniplayer: back to its watch page,
    // where compact then shows it. I is YouTube's own key for expanding the miniplayer.
    if (turnedOn && miniplayerActive()) pressKey('i', 'KeyI', 73);
    applyCompact(true);
  }

  // Ctrl+F: YouTube's own search box, not the browser's find bar.
  function searchBox() { return document.querySelector('input[name="search_query"]'); }
  function startSearch() {
    searching = true;
    applyCompact();
    var tries = 0;
    (function focusWhenShown() {
      var box = searchBox();
      if (box && box.offsetParent !== null) {
        box.focus();
        box.select();
        return;
      }
      // At a narrow width YouTube folds the box into a button, and the box it opens takes a moment
      // to render, so click once and look again for up to half a second.
      if (tries === 0) {
        var narrow = document.querySelector('#search-button-narrow button, #search-button-narrow');
        if (narrow) narrow.click();
      }
      if (++tries < 20) setTimeout(focusWhenShown, 25);
    })();
  }
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && searching && e.target === searchBox()) {
      searching = false;
      e.target.blur();
      applyCompact();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && (e.key === 'f' || e.key === 'F')) {
      e.preventDefault();
      e.stopImmediatePropagation();
      startSearch();
    }
  }, true);

  // Ctrl+V outside a text field: the text goes to the host, which opens it if it is a YouTube link
  // (YouTubeUrl.FromPastedLink decides). Inside a text field a paste is just a paste.
  function isTextField(el) {
    return !!el && (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName));
  }
  document.addEventListener('paste', function (e) {
    if (isTextField(e.target) || isTextField(document.activeElement)) return;
    var text = e.clipboardData && e.clipboardData.getData('text/plain');
    if (text) post({ paste: text });
  }, true);

  function post(msg) {
    try { window.chrome.webview.postMessage(msg); } catch (e) { /* no host bridge */ }
  }
  try {
    window.chrome.webview.addEventListener('message', function (e) {
      var d = e.data;
      if (d && typeof d.compact === 'boolean') setCompact(d.compact);
      if (d && d.search) startSearch();
      if (d && d.togglePlay) {
        var v = document.querySelector('video');
        if (v) { if (v.paused) v.play(); else v.pause(); }
      }
    });
  } catch (e) { /* no host bridge */ }
  document.addEventListener('DOMContentLoaded', function () { post({ ready: true }); });
  document.addEventListener('yt-navigate-finish', function () {
    searching = false;
    applyCompact();
    post({ navigated: location.href });
  });
})();
