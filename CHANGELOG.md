# Changelog

What changed in YtPlayer, newest first. A release is a `v*` tag on main, and the version comes from the tag.
What each SdlVulkan.Renderer version brings is in that repo's own CHANGELOG.md.

## Unreleased

- **The log says what the GPU did**: which one the renderer chose, any frame that took the GPU over its budget,
  and a fence that ran late or a device that was lost. The Adreno X1-85 has hung (Windows' GPU timeout) about twice a
  week since June, and nothing logged could say whether a frame of the player's ran long first.
- **SdlVulkan.Renderer 7.78** (from 7.72): the renderer waits for the GPU before it destroys its pipelines when the
  player closes, where it destroyed them under a frame still in flight (Vulkan's validation layer reported it at every
  exit). 7.77 also lets `SDLVK_VALIDATION=1` turn the validation layer on in this Release build, with the Vulkan SDK
  installed.

## 0.1.19 (2026-10-26)

- **The log says when the player starts and why it stops**: the strip's close button, an update, a quit from
  outside (Alt+F4, the taskbar, Windows), or the GPU given up on by the renderer, which until now ended the player
  as quietly as a quit. A start with no stop after it is a player that died without a word.

## 0.1.18 (2026-10-19)

- **A video paused before an update stays paused after it**, signed in too. YouTube plays a video again after a
  pause when it takes up where the watch history left off, which undid the single pause 0.1.16 gave it. The video
  now stays paused until you play it, or for 15 seconds.

## 0.1.17 (2026-10-19)

- **Ctrl+Q over a thumbnail queues its video** in a search's results and the home feed too. A moment over a thumbnail
  there, YouTube plays a preview over it, and Ctrl+Q found the preview rather than the video; it worked only over
  the title.

## 0.1.16 (2026-10-19)

- **An update picks up where you left off.** After updating itself the player comes back with compact as it was
  (it was always turned on), a paused video still paused, and the window still folded to its strip if it was.
  Updating from 0.1.15 keeps compact; the rest needs the old version to be this one or later.

## 0.1.15 (2026-10-19)

- **The queue and home work again.** YouTube changed how its page runs commands, and Ctrl+Q, the queue's list and
  the strip's previous in a queue said "Cannot queue on this page"; home reloaded the page and stopped the video
  playing in the background. Both go through YouTube's new way now.

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
