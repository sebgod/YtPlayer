# CLAUDE.md

A picture-in-picture YouTube player on SdlVulkan.Renderer (SDL3 + Vulkan) and SdlVulkan.Renderer.WebView
(WebView2 / WebKitGTK). README covers use and building.

- `Program.cs` wires it: a borderless, always-on-top `SdlVulkanApp` window, an `SdlEventLoop`, the web view
  placed below the strip, and SDL's hit test for move and resize.
- `PipLayout.cs` is the geometry, in WINDOW POINTS; the web view and the renderer are in pixels, so convert
  with `pixelWidth / layout.Width`. Draw and hit test both read `PipLayout`, so they cannot disagree.
- `Scripts/yt-tweaks.js` runs at document start on every page (`INativeWebView.AddScriptAtDocumentStartAsync`):
  Shorts hiding, the Shorts redirect, compact mode. Host and page talk through `chrome.webview` messages
  (`{ready}` from the page, `{compact}` from the host).
- On Linux the web view's events arrive on the GTK thread: anything that touches the window goes through
  `UiQueue`, drained on the loop thread.
- Renderer gaps get fixed upstream in SdlVulkan.Renderer, not worked around here.
- Package versions live in `Directory.Packages.props` (central package management).
