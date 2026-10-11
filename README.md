# YtPlayer

<img src="src/YtPlayer/Assets/ytplayer-256.png" width="96" align="right" alt="">

A picture-in-picture YouTube player: a small borderless window that stays on top of everything else,
showing YouTube in the platform's own browser engine (Edge WebView2 on Windows, WebKitGTK on Linux),
built on [SdlVulkan.Renderer](https://github.com/SharpAstro/SdlVulkan.Renderer) and its WebView package.

```
ytplayer                                    # YouTube's home page
ytplayer https://youtu.be/dQw4w9WgXcQ       # a video, compact
ytplayer dQw4w9WgXcQ                        # the same, by id
ytplayer lofi beats                         # a search
ytplayer --start-minimised                  # out of sight until Win+/ (for a login item)
```

## Download

Windows builds are on the [releases page](https://github.com/sebgod/YtPlayer/releases): `win-x64` for most
PCs, `win-arm64` for ARM ones (Snapdragon). Unzip anywhere and run `ytplayer.exe`; it is a native exe,
no .NET needed. It needs the Edge WebView2 Runtime, which Windows 10 and 11 already have, and a Vulkan
driver. The exe is not signed, so SmartScreen may ask before the first run ("More info", then "Run anyway").

To have it ready from login, put a shortcut to `ytplayer.exe --start-minimised` in the Startup folder
(Win+R, `shell:startup`): it starts out of sight, and Win+/ brings it up.

## The window

- **The strip along the top drags the window**; the thin border around it resizes, corners included.
  Double-click it to fold the window to the strip, and again to unfold it (Windows).
- **Back** (`<`) goes back a page.
- **Play/pause** (beside it): ▶ while paused, ❚❚ while playing; it follows the video, media keys included.
- **The title** of what is on, in the strip between the buttons.
- **Collapse** (the chevron) folds the window to its strip, the video playing on; again unfolds it to the size it had.
- **Compact** (the screen icon) shows the video alone, filling the window, with YouTube's own controls.
  Turn it off to browse. It applies only on a video's page: anywhere else YouTube shows as usual, and
  compact takes over when you open a video (the icon's red picture is outlined while it waits, filled
  while it shows). A video given on the command line opens compact. Leaving a video lets it play on in
  the background; turning compact on brings it back.
- **Stay on top** (the dot): red while the window floats above others.
- **Close** (`×`).

The taskbar icon turns red, with an equalizer in its little picture, while the video plays.

The buttons act on the first click even while another window is active. While the window is active its strip
is a lighter grey, and keys go to the page, so space or Ctrl+F reach the video after a click on the strip.

The window has **two sizes**: small while a compact video shows, bigger to browse and search in. It
switches between them as compact comes and goes, keeping the corner nearest the screen's corner in place,
and remembers each size, as you last left it, between runs; so are its position and both modes.

| Key | |
| --- | --- |
| **Win+/** | from any app: the player comes forward with the keyboard, shown again if hidden. Windows only |
| **Ctrl+H** | hides the window, off the taskbar and Alt+Tab too, the video playing on; Win+/ brings it back (if another app holds Win+/, it only minimizes) |
| **Ctrl+F** | YouTube's search box (on a compact video, compact steps aside until you pick a result or press Esc) |
| **Ctrl+V** | opens a YouTube link from the clipboard: a video, Short, playlist or channel; `youtu.be/…` without `https://` too |
| **Ctrl+C** | with nothing selected, copies the video's link (`https://youtu.be/…`); with a selection, a normal copy |
| **Ctrl+Shift+C** | copies the video's link at the current time (`…?t=83`) |
| **Ctrl+P** | play/pause, from anywhere in the window, the search box included |
| media keys, headset button | play/pause, as with YouTube in a browser |

## Signing in, and what is hidden

The web view keeps its profile in `%LOCALAPPDATA%\YtPlayer\WebView` (Windows) or
`~/.local/share/YtPlayer/WebView` (Linux), so a YouTube sign-in survives a restart. Google sometimes refuses
sign-in from an embedded browser; if it does, that is Google's check, not a bug here.

A script runs at the start of every YouTube page ([`yt-tweaks.js`](src/YtPlayer/Scripts/yt-tweaks.js)) and:

- hides Shorts: the shelves on Home, Subscriptions and search, single Shorts in feeds, the side-menu entry
  and a channel's Shorts tab;
- opens a Short as an ordinary video (`/shorts/ID` becomes `/watch?v=ID`);
- hides YouTube's own miniplayer, a picture in picture inside this one;
- presses an ad's Skip button as soon as YouTube offers it (an ad that cannot be skipped still plays). Each ad
  goes in `ytplayer.log` beside the settings (`%LOCALAPPDATA%\YtPlayer` on Windows): when it started, the
  Skip clicks, and whether it ended right after one or played out;
- in compact mode, hides end-screen cards and the pause overlay.

YouTube changes its markup often. When Shorts come back, the selector list at the top of that script is
the place to look.

## Building

Needs the .NET 10 SDK. On Linux, also WebKitGTK 4.1 (`apt install libwebkit2gtk-4.1-0`), and the window must
be on X11: run with `SDL_VIDEODRIVER=x11` (works under Wayland through XWayland). On Windows, the Edge
WebView2 Runtime, which Windows 10 and 11 already have.

```
dotnet run --project src/YtPlayer -f net10.0-windows   # Windows
dotnet run --project src/YtPlayer -f net10.0           # Linux
dotnet test --project tests/YtPlayer.Tests
```

A release is a tag: `git tag v0.1.1 && git push origin v0.1.1` on main publishes both Windows builds (native
AOT) and attaches them to that tag's GitHub Release ([`release.yml`](.github/workflows/release.yml)). To try
the same publish locally:
`dotnet publish src/YtPlayer -f net10.0-windows -r win-x64 -c Release` (or `win-arm64`; it needs the C++
build tools for that architecture).

With a checkout of SdlVulkan.Renderer next to this one (`../SdlVulkan.Renderer`), the build uses it instead
of the NuGet package (`-p:UseLocalSiblings=false` to opt out). That checkout's core project also targets
Android, so building it needs `dotnet workload install android`.

## License

[MIT](LICENSE). Each release zip also has `THIRD-PARTY-NOTICES.txt`, the licenses of what is built into the
exe or ships beside it (the .NET runtime, SDL, Microsoft's WebView2 loader, and the libraries), written for
that build by [`tools/third-party-notices.py`](tools/third-party-notices.py).
