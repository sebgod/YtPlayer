# CLAUDE.md

A picture-in-picture YouTube player on SdlVulkan.Renderer (SDL3 + Vulkan) and SdlVulkan.Renderer.WebView
(WebView2 / WebKitGTK). README covers use and building.

- `Program.cs` wires it: a borderless, always-on-top `SdlVulkanApp` window, an `SdlEventLoop`, the web view
  placed below the strip, and SDL's hit test for move and resize.
- `PipChrome.cs` is the strip and border as one DIR.Lib layout tree (`Layout.Builder`), in design units the
  widget's `DpiScale` (the display's, set on every resize) maps to pixels. The painter binds each press to the
  rect it drew, so nothing is placed by hand: DIR.Lib's `InputRouter` takes the clicks and the hover from those
  regions, SDL's hit test asks them what drags (`FrameHit`) and what resizes, and the web view goes where the
  tree's web leaf was arranged (`WebViewBounds`). Marks with an `IconKind` use it; the rest are `Fill` leaves it
  draws in their arranged rects. No scale arithmetic outside the tree: window points are pixels on Windows and
  X11, the platforms with a web view backend, so the pointer and the hit test land in the tree's pixels as they
  are. The tests paint it into DIR.Lib's `RgbaImageRenderer` and ask the regions.
- `Scripts/yt-tweaks.js` runs at document start on every page (`INativeWebView.AddScriptAtDocumentStartAsync`):
  Shorts hiding, the Shorts redirect, compact mode, the hidden miniplayer, ad skipping, the help panel, and the
  Ctrl+F, Ctrl+C, Ctrl+Shift+C, Ctrl+P, Ctrl+H, Ctrl+Up/Down, Ctrl+Q, Ctrl+L, Ctrl+V and F1 keys. Host and page talk
  through `chrome.webview` messages: `{ready}`, `{showing}`, `{paste}`, `{copy}`, `{skipAt}`, `{playing}`, `{hide}`,
  `{collapse}`, `{queueClipboard}`, `{media}`, `{help}`, `{openRelease}` from the page (read by `PageMessage`),
  `{compact}`, `{search}`, `{copyLink}`, `{togglePlay}`, `{next}`, `{previous}`, `{home}`, `{queue}`, `{listQueue}`,
  `{mediaAction}`, `{help}` from the host.
- Windows' media controls (the flyout, the lock screen, the media keys) show the player's own entry, for its window
  (`SdlVulkanWindow.TryGetMediaControls`, SdlVulkan.Renderer 7.69), named after the app id `AppIdentity.Claim`
  (AppShell 1.2) sets at start, with the Start menu shortcut that carries it. WebView2's own entry is off
  (`NativeWebViewOptions.SystemMediaControls = false`): it was "Unknown app", from `msedgewebview2.exe`. The page
  sends the track as `{media}`, read from `#movie_player.getVideoData()` whenever YouTube touches the media session
  or the player's video starts (not from the session's metadata, which hover previews and ads set too); presses come
  back as `{mediaAction}`, to the action handlers YouTube gave the session, but for previous, which is the strip's.
  To check it live, list the sessions with Windows PowerShell 5.1 (`GlobalSystemMediaTransportControlsSessionManager`);
  quick settings opened by a script (`ms-actioncenter:`) leaves the media panel out, so the flyout itself needs a
  person.
- The strip's previous goes back in a playlist, a mix or the queue when the player's `getPlaylistIndex()` is past
  the first, and otherwise starts the video over. The player's own previous button is no guide (YouTube hides it in a
  player as narrow as the window's), nor is the media session's previous (YouTube sets it for a whole mix, its first
  video included, where it does nothing). Home clicks YouTube's logo, so the page goes there without a reload and a
  video plays on in the background.
- The queue is YouTube's own: the script adds with the `addToPlaylistCommand` its "Add to queue" sends, run through
  `ytd-app.resolveCommand`, and reads `yt-playlist-manager.getPlaylistData()`. Both are YouTube internals; the
  page check covers them.
- Help (the strip's `?`, F1) is a panel in the page: the host sends `{help}` (`Program.HelpMessage`: the version,
  the update check's word, whether Win+/ is ours) and the script draws it, with the list of keys, which is the
  script's to keep in step with the keys. `UpdateCheck` asks GitHub's `releases/latest` at start once its last answer
  (kept in settings) is a day old, and each time help opens; a newer release puts a red dot on the `?`
  (`ChromeState.Update`). The panel's link sends `{openRelease}`, and the host opens the release it found, never an
  address from the page.
- A release build updates itself from the panel (`SelfUpdate`): the release workflow writes a `VERSION` marker
  (`<version> <rid>`, then the release's files) into each zip, and only a build with the marker for its own version
  offers it; one from source only links to the release. The player downloads the rid's zip into `update\` beside the
  exe, checks it against the asset's GitHub `digest` (SHA-256), and unpacks it; then `update\ytplayer.exe
  --update-from <pid> <version> [link]` waits for the player to exit, copies its files over the install, deletes the
  files the old marker lists and the new one does not (nothing else), and starts the installed exe with
  `--update-done`, which waits, deletes `update\` and runs as the player with the link. `SelfUpdate.Continue` runs
  first in `Main`, before the instance gate and any window. Each step only moves on (the staged copy acts only from
  `update\`, as the version asked for, and only starts the installed exe; --update-done starts nothing), and only a
  click starts an update. Steps log to `ytplayer.log` as `update: …`.
- Win+/ is a global hotkey (`SdlVulkanWindow.TryRegisterGlobalHotKey`, Windows only): it shows the window if
  hidden, raises it and gives it the keyboard. Ctrl+H, the strip's hide button and `--start-minimised` hide it (`SdlVulkanWindow.Hide`,
  off the taskbar too), but only while Win+/ is ours to bring it back; otherwise they minimize it.
- One player: `InstanceGate` (SharpAstro.AppShell, which comes with SdlVulkan.Renderer) is claimed at start; a
  later launch hands its arguments to the running player and exits before it has a window (`--start-minimised`
  just exits). The hand-off is drained in `OnLoopIteration`, which runs while the window is hidden too.
- YouTube's fullscreen fills the screen: the window follows `INativeWebView.FullscreenChanged` (WebView2 only),
  resizing inside that handler; `PipMode.Fullscreen` is the page with no strip or border. To test it, use
  YouTube's own fullscreen (its F, or its button), which makes the document element fullscreen. Asking for
  fullscreen on `#movie_player` directly is not what YouTube does: YouTube moves that element when the view
  grows, and the page drops out of fullscreen. The strip's fullscreen button (`PipChrome.CanFullscreen`, Windows
  only) dispatches `ytpip-fullscreen` in the page through the DevTools protocol (`Runtime.evaluate` with
  `userGesture`), on which the page script clicks YouTube's own button: a page goes fullscreen only on the user's
  gesture, and a posted message is none.
- A double-click on the strip folds the window, as the collapse button does. SDL gives the app no click on a
  draggable area, so it comes from `SdlVulkanWindow.TryReportCaptionDoubleClicks` (Windows only).
- `tools/page-check.mjs` drives that script in a headless Edge against live YouTube (search, compact,
  the keys, paste, miniplayer, the media session, next, previous and home, the strip's fullscreen, and when YouTube serves one, a real ad's Skip clicked as the host clicks it) with a stub for the host. Run it after touching the script; YouTube's markup
  moves under it.
- `WindowFit.cs`: the window's two sizes (PiP while compact is on screen, browse otherwise) and what size a
  switch gives it, in design units. The window does the rest (SdlVulkan.Renderer 7.68): `ToWindowSize` turns
  design units into its coordinates, and `ResizeInPlace` resizes it keeping the corner nearest the display's
  corner, inside the display's usable area.
- The icon is drawn by `tools/make-icon.py` into `Assets/` (the .ico is the exe's ApplicationIcon, which
  SDL uses on Windows; the raw RGBA is the X11 window icon). While the video plays the window takes the playing
  icon (`ytplayer-playing-64.rgba`: the screen red, an equalizer in the panel). Change the script, not the files.
- On Linux the web view's events arrive on the GTK thread: anything that touches the window goes through
  `UiQueue`, drained on the loop thread.
- Renderer gaps get fixed upstream in SdlVulkan.Renderer, not worked around here.
- Package versions live in `Directory.Packages.props` (central package management). What changed, and why a
  package was bumped, goes in `CHANGELOG.md`, not in comments there.
- Releases: a `v*` tag on main runs `.github/workflows/release.yml`, which AOT-publishes win-x64 and win-arm64
  and attaches the zips to the tag's GitHub Release. The version comes from the tag (`-p:Version`), not
  from the project. Each zip gets `THIRD-PARTY-NOTICES.txt` from `tools/third-party-notices.py`; a new
  package with a native file fails it until that file's license text is added to `tools/notices/`.
