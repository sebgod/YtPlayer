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
  Shorts hiding, the Shorts redirect, compact mode, the hidden miniplayer, ad skipping, and the Ctrl+F,
  Ctrl+C, Ctrl+Shift+C, Ctrl+P, Ctrl+H, Ctrl+Up/Down, Ctrl+Q, Ctrl+L and Ctrl+V keys. Host and page talk through
  `chrome.webview` messages: `{ready}`, `{showing}`, `{paste}`, `{copy}`, `{skipAt}`, `{playing}`, `{hide}`,
  `{collapse}`, `{queueClipboard}` from the page (read by `PageMessage`), `{compact}`, `{search}`, `{copyLink}`,
  `{togglePlay}`, `{next}`, `{queue}`, `{listQueue}` from the host.
- The queue is YouTube's own: the script adds with the `addToPlaylistCommand` its "Add to queue" sends, run through
  `ytd-app.resolveCommand`, and reads `yt-playlist-manager.getPlaylistData()`. Both are YouTube internals; the
  page check covers them.
- Win+/ is a global hotkey (`SdlVulkanWindow.TryRegisterGlobalHotKey`, Windows only): it shows the window if
  hidden, raises it and gives it the keyboard. Ctrl+H and `--start-minimised` hide it (`SdlVulkanWindow.Hide`,
  off the taskbar too), but only while Win+/ is ours to bring it back; otherwise they minimize it.
- One player: `InstanceGate` (SharpAstro.AppShell, which comes with SdlVulkan.Renderer) is claimed at start; a
  later launch hands its arguments to the running player and exits before it has a window (`--start-minimised`
  just exits). The hand-off is drained in `OnLoopIteration`, which runs while the window is hidden too.
- YouTube's fullscreen fills the screen: the window follows `INativeWebView.FullscreenChanged` (WebView2 only),
  resizing inside that handler; `PipMode.Fullscreen` is the page with no strip or border. To test it, use
  YouTube's own fullscreen (its F, or its button), which makes the document element fullscreen. Asking for
  fullscreen on `#movie_player` directly is not what YouTube does: YouTube moves that element when the view
  grows, and the page drops out of fullscreen.
- A double-click on the strip folds the window, as the collapse button does. SDL gives the app no click on a
  draggable area, so it comes from `SdlVulkanWindow.TryReportCaptionDoubleClicks` (Windows only).
- `tools/page-check.mjs` drives that script in a headless Edge against live YouTube (search, compact,
  the keys, paste, miniplayer, and when YouTube serves one, a real ad's Skip clicked as the host clicks it) with a stub for the host. Run it after touching the script; YouTube's markup
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
- Package versions live in `Directory.Packages.props` (central package management).
- Releases: a `v*` tag on main runs `.github/workflows/release.yml`, which AOT-publishes win-x64 and win-arm64
  and attaches the zips to the tag's GitHub Release. The version comes from the tag (`-p:Version`), not
  from the project. Each zip gets `THIRD-PARTY-NOTICES.txt` from `tools/third-party-notices.py`; a new
  package with a native file fails it until that file's license text is added to `tools/notices/`.
