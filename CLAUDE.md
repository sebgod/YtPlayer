# CLAUDE.md

A picture-in-picture YouTube player on SdlVulkan.Renderer (SDL3 + Vulkan) and SdlVulkan.Renderer.WebView
(WebView2 / WebKitGTK). README covers use and building.

- `Program.cs` wires it: a borderless, always-on-top `SdlVulkanApp` window, an `SdlEventLoop`, the web view
  placed below the strip, and SDL's hit test for move and resize.
- `PipLayout.cs` is the geometry, in WINDOW POINTS; the web view and the renderer are in pixels, so convert
  with `pixelWidth / layout.Width`. Draw and hit test both read `PipLayout`, so they cannot disagree.
- `Scripts/yt-tweaks.js` runs at document start on every page (`INativeWebView.AddScriptAtDocumentStartAsync`):
  Shorts hiding, the Shorts redirect, compact mode, the hidden miniplayer, ad skipping, and the Ctrl+F,
  Ctrl+C, Ctrl+Shift+C and Ctrl+V keys. Host and page talk through `chrome.webview` messages: `{ready}`,
  `{showing}`, `{paste}`, `{copy}` from the page (read by `PageMessage`), `{compact}`, `{search}`,
  `{copyLink}` from the host.
- `tools/page-check.mjs` drives that script in a headless Edge against live YouTube (search, compact,
  the keys, paste, miniplayer) with a stub for the host. Run it after touching the script; YouTube's markup
  moves under it.
- `WindowFit.cs`: the window's two sizes (PiP while compact is on screen, browse otherwise) and where a
  switch puts it. Logical sizes times the content scale: window coordinates are pixels on Windows and X11.
- The icon is drawn by `tools/make-icon.py` into `Assets/` (the .ico is the exe's ApplicationIcon, which
  SDL uses on Windows; the raw RGBA is the X11 window icon). Change the script, not the files.
- On Linux the web view's events arrive on the GTK thread: anything that touches the window goes through
  `UiQueue`, drained on the loop thread.
- Renderer gaps get fixed upstream in SdlVulkan.Renderer, not worked around here.
- Package versions live in `Directory.Packages.props` (central package management).
