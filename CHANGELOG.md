# Changelog

What changed in YtPlayer, newest first. A release is a `v*` tag on main, and the version comes from the tag.
What each SdlVulkan.Renderer version brings is in that repo's own CHANGELOG.md.

## 0.1.14 (2026-10-19)

- **YtPlayer updates itself.** With a newer release out, the help panel offers **Update to …**: the player
  downloads it, checks it against the checksum GitHub lists, and restarts as the new version, with the video that
  was playing at its time. Nothing shows until the new version's window. Only a release downloaded from GitHub
  updates itself (its zip now carries a `VERSION` file, which marks it); this one, 0.1.13 and before, are updated
  by hand one last time.
- **0.1.13 could crash at start** when GitHub answered its update check before the page had loaded
  (`WebView2 is not ready yet`). The answer now waits for the page.

## 0.1.13 (2026-10-19)

- **Help, in the strip and on F1.** The `?` button (and F1, in the page or on the strip) shows the keys, the version,
  and whether a newer YtPlayer is out, over the page. The player asks GitHub for its latest release once a day at
  start and each time help opens; while a newer one is out the `?` wears a red dot, and the panel links to the
  release. Nothing updates itself. The narrowest window is a button wider (348). The panel closes on its
  `×`, on Esc, or on a click outside it.

## 0.1.12 (2026-10-16)

- **The strip has hide and fullscreen buttons.** Hide (`—`) takes the window out of sight as Ctrl+H does, and
  Win+/ brings it back. Fullscreen (the four corners) is YouTube's own fullscreen, on Windows: the press reaches
  the page through the DevTools protocol, as the user's gesture a page needs to go fullscreen.
- **The player no longer crashes when the web view cannot take focus.** Activating the window (Alt+Tab, the
  taskbar, Win+/) could end it with `Win32Exception (0x800707DC): The specified tag was not found`, thrown by
  WebView2's `MoveFocus`. SdlVulkan.Renderer 7.72 reports that failure rather than throwing it
  (SharpAstro/SdlVulkan.Renderer#137).
- **A crash is written to `ytplayer.log`** with its stack, and so is an exception from a background task that
  nothing waited on. Until now a crash showed up only in Windows' event log.
