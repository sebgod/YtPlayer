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
    'ytd-miniplayer { display: none !important; }\n' +
    // The notice a copied link shows, over everything, compact included.
    '#ytpip-toast { position: fixed; left: 50%; bottom: 24px; transform: translateX(-50%);' +
    ' z-index: 2147483647; padding: 8px 14px; border-radius: 8px; background: rgba(24, 24, 28, 0.92);' +
    ' color: #fff; font: 500 14px/1.2 Roboto, Arial, sans-serif; opacity: 0; transition: opacity 0.2s;' +
    ' pointer-events: none; }\n' +
    '#ytpip-toast.ytpip-toast-on { opacity: 1; }\n' +
    // The queue's list (Ctrl+L), over everything, compact included.
    '#ytpip-queue { position: fixed; left: 8px; right: 8px; top: 8px; max-height: calc(100vh - 16px); overflow: auto;' +
    ' z-index: 2147483647; padding: 6px 0; border-radius: 8px; background: rgba(24, 24, 28, 0.96); color: #ddd;' +
    ' font: 400 13px/1.3 Roboto, Arial, sans-serif; box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5); }\n' +
    '#ytpip-queue h2 { margin: 2px 12px 6px; font: 500 13px/1.3 Roboto, Arial, sans-serif; color: #fff; }\n' +
    '#ytpip-queue .ytpip-q { display: block; width: 100%; padding: 4px 12px; border: 0; background: none; color: inherit;' +
    ' font: inherit; text-align: left; cursor: pointer; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }\n' +
    '#ytpip-queue .ytpip-q:hover { background: rgba(255, 255, 255, 0.1); }\n' +
    '#ytpip-queue .ytpip-q-now { color: #ff3030; }\n' +
    // The help panel (the strip's ? and F1): the keys, the version and whether a newer one is out, over everything.
    '#ytpip-help { position: fixed; left: 8px; right: 8px; top: 8px; max-height: calc(100vh - 16px); overflow: auto;' +
    ' z-index: 2147483647; padding: 10px 14px; border-radius: 8px; background: rgba(24, 24, 28, 0.96); color: #ddd;' +
    ' font: 400 13px/1.35 Roboto, Arial, sans-serif; box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5); }\n' +
    '#ytpip-help .ytpip-help-close { position: absolute; top: 4px; right: 6px; width: 24px; height: 24px; padding: 0;' +
    ' border: 0; border-radius: 4px; background: none; color: #aaa; font: 400 18px/24px Roboto, Arial, sans-serif;' +
    ' cursor: pointer; }\n' +
    '#ytpip-help .ytpip-help-close:hover { background: rgba(255, 255, 255, 0.1); color: #fff; }\n' +
    '#ytpip-help h2 { margin: 0 0 2px; font: 500 14px/1.3 Roboto, Arial, sans-serif; color: #fff; }\n' +
    '#ytpip-help .ytpip-help-update { margin: 0 0 8px; color: #aaa; }\n' +
    '#ytpip-help .ytpip-help-newer { padding: 0; border: 0; background: none; color: #ff3030; font: inherit;' +
    ' text-decoration: underline; cursor: pointer; }\n' +
    '#ytpip-help table { border-collapse: collapse; }\n' +
    '#ytpip-help td { padding: 1px 0; vertical-align: top; }\n' +
    '#ytpip-help td:first-child { padding-right: 14px; white-space: nowrap; color: #fff; font-weight: 500; }\n' +
    '#ytpip-help .ytpip-help-foot { margin: 8px 0 0; color: #888; }\n';

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

  // An ad: once YouTube offers its Skip button, press it, the click the viewer would make. YouTube
  // ignores a click from a page script (event.isTrusted is false), so the page only says where the
  // button is and the host clicks there through the browser, a trusted click (WebView2 only). Nothing is
  // blocked; an ad that cannot be skipped, and the seconds before Skip appears, play as usual.
  // The newer ad overlay holds its Skip button (or, for an ad that cannot be skipped, the countdown to
  // the video) in a skip-or-preview container; the older class names are kept for the older layout.
  var SKIP_BUTTON = '.ytp-skip-ad-button, .ytp-ad-skip-button, .ytp-ad-skip-button-modern,' +
    ' .ytp-ad-player-overlay-layout__skip-or-preview-container button[class*="skip"]';
  var lastSkipAt = 0;
  // Where a click would press the button, or null while it is not really shown. YouTube puts the button
  // in the page before it shows it (one was there at x 0 with no width, 0.6 s into an ad that then ran
  // its 16 s), and a click there presses nothing. So it must have a size, be visible and opaque, and be
  // what a click at its centre lands on: the trusted click hits whatever elementFromPoint finds there.
  // Its box, not offsetParent: that is null for a fixed-position element even when it shows.
  function skipTarget(skip) {
    if (skip.disabled) return null;
    var r = skip.getBoundingClientRect();
    if (r.width < 8 || r.height < 8) return null;
    if (skip.checkVisibility && !skip.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return null;
    var x = r.x + r.width / 2, y = r.y + r.height / 2;
    var hit = document.elementFromPoint(x, y);
    return hit && skip.contains(hit) ? { x: x, y: y } : null;
  }
  // The ad's start and end go to the host too, for its log (did a click skip it, or did it play out).
  var adShowing = false;
  setInterval(function () {
    var ad = !!document.querySelector('#movie_player.ad-showing');
    if (ad !== adShowing) {
      adShowing = ad;
      post({ ad: ad });
    }
    // Asked again a second later if the ad is still there, in case a click landed mid-animation.
    if (!ad || Date.now() - lastSkipAt < 1000) return;
    var buttons = document.querySelectorAll(SKIP_BUTTON);
    for (var i = 0; i < buttons.length; i++) {
      var at = skipTarget(buttons[i]);
      if (!at) continue;
      lastSkipAt = Date.now();
      post({ skipAt: at });
      return;
    }
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

  // Ctrl+C with nothing selected copies the video's link; Ctrl+Shift+C, the link at the current time
  // (which also keeps the browser's element inspector, on that key, from opening). The video is the
  // player's, so a video playing on in the background while browsing is the one copied; with no video,
  // the page's address. The host puts the link on the clipboard: the page's own clipboard access hangs
  // on permissions a web view may not grant.
  function playerVideo() {
    var player = document.getElementById('movie_player');
    try {
      var data = player && player.getVideoData && player.getVideoData();
      if (data && data.video_id) return { id: data.video_id, time: Math.floor(player.getCurrentTime() || 0) };
    } catch (e) { /* not YouTube's player API after all */ }
    var id = isWatchPage() && new URLSearchParams(location.search).get('v');
    var video = document.querySelector('video');
    return id ? { id: id, time: video ? Math.floor(video.currentTime) : 0 } : null;
  }
  function clock(seconds) {
    var h = Math.floor(seconds / 3600), m = Math.floor(seconds / 60) % 60, s = seconds % 60;
    var mm = h ? (m < 10 ? '0' : '') + m : String(m);
    return (h ? h + ':' : '') + mm + ':' + (s < 10 ? '0' : '') + s;
  }
  function toast(text) {
    var el = document.getElementById('ytpip-toast');
    if (!el) {
      el = document.createElement('div');
      el.id = 'ytpip-toast';
      document.documentElement.appendChild(el);
    }
    el.textContent = text;
    el.classList.add('ytpip-toast-on');
    clearTimeout(toast.timer);
    toast.timer = setTimeout(function () { el.classList.remove('ytpip-toast-on'); }, 1500);
  }
  function copyLink(atTime) {
    var video = playerVideo();
    var timed = !!video && atTime && video.time > 0;
    post({ copy: video ? 'https://youtu.be/' + video.id + (timed ? '?t=' + video.time : '') : location.href });
    toast(timed ? 'Link at ' + clock(video.time) + ' copied' : 'Link copied');
  }
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey || (e.key !== 'c' && e.key !== 'C')) return;
    if (!e.shiftKey) {
      // A plain Ctrl+C is a copy wherever there is something to copy.
      var selection = window.getSelection();
      if (isTextField(document.activeElement) || (selection && String(selection).trim())) return;
    }
    e.preventDefault();
    e.stopImmediatePropagation();
    copyLink(e.shiftKey);
  }, true);

  // Ctrl+V outside a text field: the text goes to the host, which opens it if it is a YouTube link
  // (YouTubeUrl.FromPastedLink decides). Inside a text field a paste is just a paste.
  function isTextField(el) {
    return !!el && (el.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName));
  }
  // Ctrl+P plays or pauses, from anywhere in the window, the search box included (the browser would print).
  // The host's play/pause button and the strip's Ctrl+P come here too, as {togglePlay}.
  function togglePlay(notify) {
    // The player's video, wherever it is (the hidden miniplayer too), not a hover preview's.
    var v = document.querySelector('#movie_player video');
    if (!v) return;
    if (v.paused) v.play(); else v.pause();
    if (notify) toast(v.paused ? 'Paused' : 'Playing');
  }
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey || (e.key !== 'p' && e.key !== 'P')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    togglePlay(true);
  }, true);

  // Ctrl+Up folds the window to its strip, as the strip's chevron does (the host does it); Ctrl+Down, which the
  // strip takes once folded, unfolds it. From anywhere in the window, as Ctrl+P plays.
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey || (e.key !== 'ArrowUp' && e.key !== 'ArrowDown')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    post({ collapse: e.key === 'ArrowUp' });
  }, true);

  // The strip's next button (and Shift+N on the folded strip): the player's own next, a playlist's next or
  // autoplay's pick. YouTube's own Shift+N does the same in the page.
  function playNext() {
    var next = document.querySelector('#movie_player .ytp-next-button');
    if (next && next.getAttribute('aria-disabled') !== 'true' && getComputedStyle(next).display !== 'none') {
      next.click();
      return;
    }
    var player = document.getElementById('movie_player');
    try {
      if (player && player.nextVideo) player.nextVideo();
    } catch (e) { /* not YouTube's player API after all */ }
  }
  // The strip's previous button (and Shift+P on the folded strip, and Windows' previous): the previous video where
  // there is one (in a playlist, a mix or the queue, past its first), and where there is none, the video from its
  // start. The player's playlist index says which: the player's own previous button is hidden in a player as narrow as
  // the window's, and the media session's previous is set for a whole mix, its first video included, where it does
  // nothing.
  function playPrevious() {
    var player = document.getElementById('movie_player');
    try {
      if (player && player.getPlaylistIndex && player.getPlaylistIndex() > 0) {
        player.previousVideo();
        return;
      }
    } catch (e) { /* not YouTube's player API after all */ }
    var v = document.querySelector('#movie_player video');
    if (v) v.currentTime = 0;
  }

  // YouTube runs a command (an endpoint, a signal) through its app: ytd-app.resolveCommand until October 2026, and
  // since then handleCommandWithCommandHandler, which hands it to the same resolver. False when neither is there.
  function runCommand(command) {
    var app = document.querySelector('ytd-app');
    if (app && app.handleCommandWithCommandHandler) app.handleCommandWithCommandHandler(command, app);
    else if (app && app.resolveCommand) app.resolveCommand(command);
    else return false;
    return true;
  }

  // The strip's home button: YouTube's home, the way its logo goes there, within the page, so a video plays on in
  // the background as it does when leaving it by any other link. The app's own home action first: since October 2026
  // a click on the logo reloads the page. Away from YouTube (a sign-in page), a load of it.
  function goHome() {
    var onYouTube = location.hostname === 'www.youtube.com';
    var app = onYouTube && document.querySelector('ytd-app');
    if (app && app.onYtNavigateHomeAction) {
      try { app.onYtNavigateHomeAction(); return; } catch (e) { /* not YouTube's app after all */ }
    }
    var logo = onYouTube && document.querySelector('ytd-topbar-logo-renderer a#logo');
    if (logo) logo.click();
    else location.assign('https://www.youtube.com/');
  }

  // The queue is YouTube's own, the one its "Add to queue" fills, kept by its playlist manager while the page
  // lasts (a full reload drops it). Ctrl+Q adds the video under the pointer (a thumbnail in a list), or with
  // none there the one whose link is on the clipboard: the host reads it and sends it back as {queue}.
  // Ctrl+L lists the queue (or the mix or playlist that plays), and a click on an entry plays it.
  var pointer = null;
  document.addEventListener('mousemove', function (e) { pointer = { x: e.clientX, y: e.clientY }; }, true);
  var VIDEO_TILE = 'ytd-video-renderer, ytd-rich-item-renderer, ytd-compact-video-renderer, ytd-grid-video-renderer,' +
    ' yt-lockup-view-model, ytd-playlist-panel-video-renderer, ytd-playlist-video-renderer, ytd-reel-item-renderer';
  function videoUnderPointer() {
    var el = pointer && document.elementFromPoint(pointer.x, pointer.y);
    var tile = el && el.closest && el.closest(VIDEO_TILE);
    var link = tile && tile.querySelector('a[href*="watch?v="], a[href*="/shorts/"]');
    var m = link && /[?&]v=([\w-]{11})|\/shorts\/([\w-]{11})/.exec(link.getAttribute('href') || '');
    if (!m) return null;
    var title = tile.querySelector('#video-title, a[title], h3');
    return { id: m[1] || m[2], title: title ? (title.getAttribute('title') || title.textContent || '').trim() : '' };
  }
  function playlistData() {
    var manager = document.querySelector('yt-playlist-manager');
    try {
      return manager && manager.getPlaylistData ? manager.getPlaylistData() : null;
    } catch (e) { return null; /* not YouTube's playlist manager after all */ }
  }
  function playlistEntries(data) {
    return ((data && data.contents) || []).map(function (c) { return c.playlistPanelVideoRenderer; }).filter(Boolean);
  }
  function textOf(t) {
    return typeof t === 'string' ? t : t ? t.simpleText || (t.runs || []).map(function (r) { return r.text; }).join('') : '';
  }
  // The command YouTube's own "Add to queue" sends, built from the id alone.
  function queueVideo(id, title) {
    try {
      var ran = runCommand({ signalServiceEndpoint: { signal: 'CLIENT_SIGNAL', actions: [{ addToPlaylistCommand: {
        openMiniplayer: false, videoId: id, listType: 'PLAYLIST_EDIT_LIST_TYPE_QUEUE', videoIds: [id],
        onCreateListCommand: {
          commandMetadata: { webCommandMetadata: { sendPost: true, apiUrl: '/youtubei/v1/playlist/create' } },
          createPlaylistServiceEndpoint: { videoIds: [id], params: 'CAQ%3D' } },
        videoCommand: {
          commandMetadata: { webCommandMetadata: { url: '/watch?v=' + id, webPageType: 'WEB_PAGE_TYPE_WATCH', rootVe: 3832 } },
          watchEndpoint: { videoId: id } } } }] } });
      if (!ran) { toast('Cannot queue on this page'); return; }
    } catch (e) { toast('Could not queue it'); return; }
    // The queue takes the entry a little later (longer when it first turns a mix into a queue), with the title a
    // link did not carry; the notice waits for it, up to three seconds.
    var tries = 0;
    (function confirm() {
      var list = playlistEntries(playlistData());
      var added = list.filter(function (v) { return v.videoId === id; }).pop();
      if (!added && ++tries < 15) { setTimeout(confirm, 200); return; }
      toast((added ? 'Queued: ' : 'Queued, perhaps: ') + (title || (added ? textOf(added.title) : id))
        + (added && list.length > 1 ? ' (' + list.length + ' in the queue)' : ''));
    })();
  }
  function closeQueueList() {
    var el = document.getElementById('ytpip-queue');
    if (el) el.remove();
  }
  function showQueueList(toggle) {
    if (document.getElementById('ytpip-queue')) {
      if (toggle) closeQueueList();
      return;
    }
    var data = playlistData();
    var list = playlistEntries(data);
    if (!list.length) { toast('The queue is empty'); return; }
    var panel = document.createElement('div');
    panel.id = 'ytpip-queue';
    var heading = document.createElement('h2');
    heading.textContent = (textOf(data.title) || 'Queue') + ' (' + list.length + ')';
    panel.appendChild(heading);
    list.forEach(function (v, i) {
      var entry = document.createElement('button');
      entry.className = 'ytpip-q' + (v.selected ? ' ytpip-q-now' : '');
      entry.textContent = (v.selected ? '\u25b6 ' : (i + 1) + '. ') + (textOf(v.title) || v.videoId);
      entry.addEventListener('click', function () {
        closeQueueList();
        if (v.navigationEndpoint) runCommand(v.navigationEndpoint);
      });
      panel.appendChild(entry);
    });
    document.documentElement.appendChild(panel);
    var now = panel.querySelector('.ytpip-q-now');
    if (now) now.scrollIntoView({ block: 'nearest' });
  }
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey) return;
    var key = e.key.toLowerCase();
    if (key !== 'q' && key !== 'l') return;
    e.preventDefault();
    e.stopImmediatePropagation();
    if (key === 'l') {
      showQueueList(true);
      return;
    }
    var video = videoUnderPointer();
    if (video) queueVideo(video.id, video.title);
    else post({ queueClipboard: true });
  }, true);
  // Escape, a click anywhere else, or the next page closes the list.
  document.addEventListener('keydown', function (e) {
    if (e.key !== 'Escape' || !document.getElementById('ytpip-queue')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    closeQueueList();
  }, true);
  document.addEventListener('mousedown', function (e) {
    var el = document.getElementById('ytpip-queue');
    if (el && !el.contains(e.target)) closeQueueList();
  }, true);
  document.addEventListener('yt-navigate-start', closeQueueList);

  // Ctrl+H hides the window (the host does it; Win+/ brings it back), from anywhere in it, as Ctrl+P plays.
  document.addEventListener('keydown', function (e) {
    if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey || (e.key !== 'h' && e.key !== 'H')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    post({ hide: true });
  }, true);

  // The help panel: F1 or the strip's ? (the host's {help}, which brings the version and the update check's answer).
  // The keys are this script's and the host's; the README has the same list.
  function helpKeys(summon) {
    return [
      ['F1', 'this help'],
      summon ? ['Win+/', 'the player comes forward, from any app'] : null,
      ['Ctrl+H', summon ? 'hide the window; Win+/ brings it back' : 'minimize the window'],
      ['Ctrl+F', "YouTube's search"],
      ['Ctrl+P', 'play / pause'],
      ['Shift+N / Shift+P', 'next / previous video'],
      ['Ctrl+V', 'open a YouTube link from the clipboard'],
      ['Ctrl+C', "copy the video's link (with nothing selected)"],
      ['Ctrl+Shift+C', "copy the video's link at the current time"],
      ['Ctrl+Q', 'queue the video under the pointer, or the copied link'],
      ['Ctrl+L', 'list the queue'],
      ['Ctrl+Up / Ctrl+Down', 'fold the window to its strip / unfold it'],
      ['F', "YouTube's fullscreen"],
      ['Esc', 'close this, the queue or the search']
    ].filter(Boolean);
  }
  function closeHelp() {
    var el = document.getElementById('ytpip-help');
    if (el) el.remove();
  }
  // What plays, at its time, for the player to open again once it has updated itself.
  function resumeLink() {
    var video = playerVideo();
    return video ? 'https://youtu.be/' + video.id + (video.time > 0 ? '?t=' + video.time : '') : location.href;
  }
  function helpLink(text, onClick) {
    var link = document.createElement('button');
    link.className = 'ytpip-help-newer';
    link.textContent = text;
    link.addEventListener('click', onClick);
    return link;
  }
  // The host opens the release it found and downloads the update it found; the page names no address.
  function updateLine(h) {
    var line = document.createElement('p');
    line.className = 'ytpip-help-update';
    var release = function () { post({ openRelease: true }); };
    if (h.update === 'newer' && h.newer && h.canUpdate) {
      line.appendChild(helpLink('Update to ' + h.newer, function () {
        var v = document.querySelector('#movie_player video');
        post({ update: true, resume: resumeLink(), paused: !!v && v.paused });
      }));
      line.appendChild(document.createTextNode(' · '));
      line.appendChild(helpLink("what's new", release));
    } else if (h.update === 'newer' && h.newer) {
      line.appendChild(helpLink('YtPlayer ' + h.newer + ' is out: get it', release));
    } else if (h.update === 'downloading') {
      line.textContent = 'Downloading the update… the player restarts with it';
    } else if (h.update === 'failed') {
      line.textContent = 'The update failed (ytplayer.log says why)';
      if (h.newer) {
        line.appendChild(document.createTextNode(' · '));
        line.appendChild(helpLink('get it from its page', release));
      }
    } else {
      line.textContent = h.update === 'checking' ? 'Looking for a newer version…'
        : h.update === 'current' ? 'Up to date'
        : 'Could not look for a newer version';
    }
    return line;
  }
  function renderHelp(panel, h) {
    panel.textContent = '';
    // The × closes it, as Esc and a click outside do.
    var close = document.createElement('button');
    close.className = 'ytpip-help-close';
    close.title = 'Close (Esc)';
    close.textContent = '\u00d7';
    close.addEventListener('click', closeHelp);
    panel.appendChild(close);
    var heading = document.createElement('h2');
    heading.textContent = 'YtPlayer ' + h.version;
    panel.appendChild(heading);
    panel.appendChild(updateLine(h));
    var table = document.createElement('table');
    helpKeys(h.summon).forEach(function (k) {
      var row = table.insertRow();
      row.insertCell().textContent = k[0];
      row.insertCell().textContent = k[1];
    });
    panel.appendChild(table);
    var foot = document.createElement('p');
    foot.className = 'ytpip-help-foot';
    foot.textContent = "YouTube's own keys: Shift+/ in the page.";
    panel.appendChild(foot);
  }
  // open: the panel opens, or closes if it is open. Otherwise an open one takes the host's news (the update check's
  // answer), and a closed one stays closed.
  function showHelp(h) {
    var panel = document.getElementById('ytpip-help');
    if (h.open && panel) { panel.remove(); return; }
    if (!h.open && !panel) return;
    if (!panel) {
      closeQueueList();
      panel = document.createElement('div');
      panel.id = 'ytpip-help';
      document.documentElement.appendChild(panel);
    }
    renderHelp(panel, h);
  }
  document.addEventListener('keydown', function (e) {
    if (e.key === 'F1' && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey) {
      e.preventDefault();
      e.stopImmediatePropagation();
      post({ help: true });
      return;
    }
    if (e.key !== 'Escape' || !document.getElementById('ytpip-help')) return;
    e.preventDefault();
    e.stopImmediatePropagation();
    closeHelp();
  }, true);
  // A click anywhere else closes it, as it does the queue's list.
  document.addEventListener('mousedown', function (e) {
    var el = document.getElementById('ytpip-help');
    if (el && !el.contains(e.target)) closeHelp();
  }, true);

  document.addEventListener('paste', function (e) {
    if (isTextField(e.target) || isTextField(document.activeElement)) return;
    var text = e.clipboardData && e.clipboardData.getData('text/plain');
    if (text) post({ paste: text });
  }, true);

  // The strip's fullscreen button: YouTube's own fullscreen, as its button gives it. The host dispatches this event
  // through the DevTools protocol as the user's gesture, which a page may go fullscreen on; a posted message is not one.
  window.addEventListener('ytpip-fullscreen', function () {
    var button = isWatchPage() && document.querySelector('#movie_player .ytp-fullscreen-button');
    if (button) button.click();
    else toast('Open a video to watch it fullscreen');
  });

  function post(msg) {
    try { window.chrome.webview.postMessage(msg); } catch (e) { /* no host bridge */ }
  }
  try {
    window.chrome.webview.addEventListener('message', function (e) {
      var d = e.data;
      if (d && typeof d.compact === 'boolean') setCompact(d.compact);
      if (d && d.search) startSearch();
      if (d && d.copyLink) copyLink(d.copyLink === 'time');
      // From a key (the strip's Ctrl+P) it says what it did; from the strip's button, which shows it, not.
      if (d && d.togglePlay) togglePlay(d.togglePlay === 'key');
      if (d && d.next) playNext();
      if (d && d.previous) playPrevious();
      if (d && d.home) goHome();
      // The strip's Ctrl+Q: the video whose link was on the clipboard, or null when there was none.
      if (d && 'queue' in d) {
        if (d.queue) queueVideo(d.queue, '');
        else toast('Point at a video, or copy its link, to queue it');
      }
      if (d && d.listQueue) showQueueList(false);
      if (d && d.mediaAction) mediaAction(d.mediaAction);
      if (d && d.help && typeof d.help === 'object') showHelp(d.help);
      if (d && d.startPaused) pauseOnStart();
    });
  } catch (e) { /* no host bridge */ }
  // Whether the player's video plays, for the strip's play/pause button. Media events do not bubble, but
  // a capturing listener on the document still sees them; a hover preview's video is not the player's.
  function reportPlaying(e) {
    var v = e.target;
    if (!v || v.tagName !== 'VIDEO' || !v.closest('#movie_player')) return;
    post({ playing: !v.paused && !v.ended });
    // A new video in the player says so here too, should YouTube set the session's metadata before the player's.
    reportMedia();
  }
  // Back from an update that left the video paused ({startPaused}): the video, not an ad before it, is paused as it
  // starts, once; YouTube autoplays a video it opens.
  var startPaused = false;
  function pauseIfAsked() {
    var v = document.querySelector('#movie_player video');
    if (!startPaused || !v || v.paused || document.querySelector('#movie_player.ad-showing')) return;
    startPaused = false;
    v.pause();
  }
  function pauseOnStart() {
    startPaused = true;
    pauseIfAsked();
  }
  document.addEventListener('playing', pauseIfAsked, true);
  ['playing', 'pause', 'ended', 'emptied'].forEach(function (type) {
    document.addEventListener(type, reportPlaying, true);
  });

  // Windows' media controls (the flyout, the lock screen, the media keys) are the host's, for its window and under
  // its name; the browser's own are off. The track goes to the host as {media} (null while the player has no
  // video), each time it changes, and the host's presses come back as {mediaAction}, to the handlers YouTube gave
  // the browser's media session, as the browser would have sent them.
  // The track is the player's own video, read from the player. YouTube's media session is only the cue to look:
  // a hover preview and an ad set its metadata too, and neither is what plays.
  var mediaHandlers = {};
  var mediaSent;
  function reportMedia() {
    var player = document.getElementById('movie_player');
    var data = null;
    try {
      data = player && player.getVideoData ? player.getVideoData() : null;
    } catch (e) { /* not YouTube's player API after all */ }
    var media = data && data.title && data.video_id ? {
      title: data.title, artist: data.author || '',
      // The video's own picture, which every video has, as JPEG: Windows may have no WebP decoder.
      artwork: 'https://i.ytimg.com/vi/' + data.video_id + '/hqdefault.jpg',
      // Both always, as the strip's buttons: previous starts the video over where YouTube has no previous video.
      previous: true, next: true
    } : null;
    var json = JSON.stringify(media);
    if (json === mediaSent) return;
    mediaSent = json;
    post({ media: media });
  }
  try {
    var session = MediaSession.prototype;
    var metadata = Object.getOwnPropertyDescriptor(session, 'metadata');
    Object.defineProperty(session, 'metadata', {
      configurable: true, enumerable: metadata.enumerable, get: metadata.get,
      set: function (value) { metadata.set.call(this, value); reportMedia(); }
    });
    var setActionHandler = session.setActionHandler;
    session.setActionHandler = function (action, handler) {
      setActionHandler.call(this, action, handler); // throws for an action the browser does not know, as before
      mediaHandlers[action] = handler || null;
      reportMedia();
    };
  } catch (e) { /* no media session in this browser */ }
  function mediaAction(action) {
    // Previous is the strip's previous, which goes to YouTube's where it shows one.
    if (action === 'previoustrack') {
      playPrevious();
      return;
    }
    var handler = mediaHandlers[action];
    if (handler) {
      try { handler({ action: action }); } catch (e) { /* YouTube's own */ }
      return;
    }
    // An action YouTube left to the browser: the player's video plays or pauses; next is the strip's next.
    if (action === 'nexttrack') {
      playNext();
      return;
    }
    var v = document.querySelector('#movie_player video');
    if (!v) return;
    if (action === 'play') v.play();
    else if (action === 'pause' || action === 'stop') v.pause();
  }
  document.addEventListener('DOMContentLoaded', function () { post({ ready: true }); });
  document.addEventListener('yt-navigate-finish', function () {
    searching = false;
    applyCompact();
    post({ navigated: location.href });
  });
})();
