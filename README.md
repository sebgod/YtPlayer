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

There is one player: run it again, from the Start menu, a shortcut or the command line, and the running one comes
forward, with the video or search it was given. `--start-minimised` leaves a running one as it is.

## Download

Windows builds are on the [releases page](https://github.com/sebgod/YtPlayer/releases): `win-x64` for most
PCs, `win-arm64` for ARM ones (Snapdragon). Unzip anywhere and run `ytplayer.exe`; it is a native exe,
no .NET needed. It needs the Edge WebView2 Runtime, which Windows 10 and 11 already have, and a Vulkan
driver. The exe is not signed, so SmartScreen may ask before the first run ("More info", then "Run anyway").
The first run puts YtPlayer in the Start menu (a shortcut, which is also how Windows knows the player's name); move
the folder later and the next run moves the shortcut with it.

To have it ready from login, put a shortcut to `ytplayer.exe --start-minimised` in the Startup folder
(Win+R, `shell:startup`): it starts out of sight, and Win+/ brings it up, as does starting it again from the Start menu.

## The window

- **The strip along the top drags the window**; the thin border around it resizes, corners included.
  Double-click it to fold the window to the strip, and again to unfold it (Windows).
- **Back** (←) goes back a page, and **Home** (⌂, beside it) to YouTube's home. A video playing plays on in the
  background, as it does when you leave it any other way.
- **Previous** (❘◀): the previous video in a playlist, a mix or the queue; anywhere else, the video from its start.
- **Play/pause** (beside it): ▶ while paused, ❚❚ while playing; it follows the video, media keys included.
- **Next** (▶❘, beside that): the next video, a playlist's next or YouTube's autoplay pick.
- **The title** of what is on, in the strip between the buttons.
- **Collapse** (▲, ▼ while folded) folds the window to its strip, the video playing on; again unfolds it to the size it had.
  Ctrl+Up and Ctrl+Down do the same.
- **Compact** (the screen icon) shows the video alone, filling the window, with YouTube's own controls.
  Turn it off to browse. It applies only on a video's page: anywhere else YouTube shows as usual, and
  compact takes over when you open a video (the icon's red picture is outlined while it waits, filled
  while it shows). A video given on the command line opens compact. Leaving a video lets it play on in
  the background; turning compact on brings it back.
- **Stay on top** (the dot): red while the window floats above others.
- **Help** (`?`, or F1) shows the keys, the version, and whether a newer one is out, over the page; Esc or a click
  elsewhere closes it. YtPlayer asks GitHub for its latest release once a day at start and each time help opens;
  while a newer one is out the `?` has a red dot. **Update to …** in the panel updates YtPlayer where it is: it
  downloads the release, checks it against the checksum GitHub lists, and restarts as the new version with the
  video that was playing, at its time, paused if it was, compact as it was, and folded if it was. That is for a release downloaded from GitHub (Windows); a build of your own
  only links to the release page.
- **Hide** (`—`) takes the window out of sight, off the taskbar and Alt+Tab too, the video playing on, as Ctrl+H
  does. Win+/ brings it back, and so does starting YtPlayer again. If another app holds Win+/, it only minimizes.
- **Fullscreen** (the four corners) is YouTube's fullscreen, as its own button or F gives it, on a video's page
  (Windows).
- **Close** (`×`) quits.

The taskbar icon turns red, with an equalizer in its little picture, while the video plays.

Windows' **media controls** (in quick settings, beside the volume when it changes, and on the lock screen) show the
video as YtPlayer's: its title, channel and picture, with play/pause, previous and next, which do what the strip's do
(Windows).

The **queue** is YouTube's own, the one its "Add to queue" fills, so the next button and autoplay follow it. Like
YouTube's, it lasts while the page does: opening a link with Ctrl+V or from the command line starts a new page
without it.

YouTube's **fullscreen** (F, its button, a double-click on the video, the strip's) fills the screen, not just the window
(Windows); Esc or F again puts the window back where it was.

The buttons act on the first click even while another window is active. While the window is active its strip
is a lighter grey, and keys go to the page, so space or Ctrl+F reach the video after a click on the strip.

The window has **two sizes**: small while a compact video shows, bigger to browse and search in. It
switches between them as compact comes and goes, keeping the corner nearest the screen's corner in place,
and remembers each size, as you last left it, between runs; so are its position and both modes.

| Key | |
| --- | --- |
| **F1** | the help panel: these keys, the version, and whether a newer one is out |
| **Win+/** | from any app: the player comes forward with the keyboard, shown again if hidden. Windows only |
| **Ctrl+H** | hides the window, off the taskbar and Alt+Tab too, the video playing on; Win+/ brings it back (if another app holds Win+/, it only minimizes) |
| **Ctrl+F** | YouTube's search box (on a compact video, compact steps aside until you pick a result or press Esc) |
| **Ctrl+V** | opens a YouTube link from the clipboard: a video, Short, playlist or channel; `youtu.be/…` without `https://` too |
| **Ctrl+C** | with nothing selected, copies the video's link (`https://youtu.be/…`); with a selection, a normal copy |
| **Ctrl+Shift+C** | copies the video's link at the current time (`…?t=83`) |
| **Ctrl+P** | play/pause, from anywhere in the window, the search box included |
| **Ctrl+Up** / **Ctrl+Down** | fold the window to its strip / unfold it |
| **Shift+N** | the next video (YouTube's own key; on the folded strip too) |
| **Shift+P** | the previous video in a playlist (YouTube's own key); on the folded strip, what the previous button does |
| **Ctrl+Q** | queues the video under the pointer (a thumbnail), or else the one whose link is on the clipboard |
| **Ctrl+L** | lists the queue (or the mix or playlist that plays); click an entry to play it, Esc closes the list |
| **F** | YouTube's fullscreen, which fills the screen; Esc or F again leaves it |
| media keys, headset button | play/pause, next and previous, as with YouTube in a browser |

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
