# YtPlayer

A picture-in-picture YouTube player: a small borderless window that stays on top of everything else,
showing YouTube in the platform's own browser engine (Edge WebView2 on Windows, WebKitGTK on Linux),
built on [SdlVulkan.Renderer](https://github.com/SharpAstro/SdlVulkan.Renderer) and its WebView package.

```
ytplayer                                    # YouTube's home page
ytplayer https://youtu.be/dQw4w9WgXcQ       # a video, compact
ytplayer dQw4w9WgXcQ                        # the same, by id
ytplayer lofi beats                         # a search
```

## The window

- **The strip along the top drags the window**; the thin border around it resizes, corners included.
- **Back** (`<`) goes back a page.
- **Compact** (the screen icon) shows the video alone, filling the window, with YouTube's own controls.
  Turn it off to browse. It applies only on a video's page: anywhere else YouTube shows as usual, and
  compact takes over when you open a video. A video given on the command line opens compact.
- **Stay on top** (the dot): red while the window floats above others.
- **Close** (`×`).

Position, size and both modes are remembered between runs.

## Signing in, and what is hidden

The web view keeps its profile in `%LOCALAPPDATA%\YtPlayer\WebView` (Windows) or
`~/.local/share/YtPlayer/WebView` (Linux), so a YouTube sign-in survives a restart. Google sometimes refuses
sign-in from an embedded browser; if it does, that is Google's check, not a bug here.

A script runs at the start of every YouTube page ([`yt-tweaks.js`](src/YtPlayer/Scripts/yt-tweaks.js)) and:

- hides Shorts: the shelves on Home, Subscriptions and search, single Shorts in feeds, the side-menu entry
  and a channel's Shorts tab;
- opens a Short as an ordinary video (`/shorts/ID` becomes `/watch?v=ID`);
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

With a checkout of SdlVulkan.Renderer next to this one (`../SdlVulkan.Renderer`), the build uses it instead
of the NuGet package (`-p:UseLocalSiblings=false` to opt out). That checkout's core project also targets
Android, so building it needs `dotnet workload install android`.
