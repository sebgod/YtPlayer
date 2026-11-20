using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DIR.Lib;
using SdlVulkan.Renderer;
using SdlVulkan.Renderer.WebView;
using SharpAstro.AppShell;
using static SDL3.SDL;

namespace YtPlayer;

/// <summary>
/// A picture-in-picture YouTube player: a small borderless window that stays on top, showing YouTube
/// in the platform's web view (WebView2 on Windows, WebKitGTK on Linux) under a thin strip that drags
/// the window and holds its buttons: back, play/pause, next, collapse, compact (the video alone), stay on top, help,
/// close.
/// </summary>
/// <remarks>Usage: <c>ytplayer [--start-minimised] [video URL | video id | search words]</c>. With a player already
/// running, it gets the video or search instead, and comes forward.</remarks>
internal static class Program
{
    private static readonly ConcurrentQueue<Action> UiQueue = new();
    private static HitTest? _hitTest; // held for as long as SDL may call it
    private const int SummonHotKey = 1; // Win+/
    private const string AppId = "sebgod.YtPlayer"; // the app id Windows knows the player by

    [STAThread] // WebView2 requires an STA thread
    private static int Main(string[] args)
    {
        // A windowed app has no console to die on: whatever takes the player down goes to its log first, with
        // the stack, and a task's exception nobody awaited is logged too rather than lost.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => PlayerLog.Write($"crash: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => PlayerLog.Write($"unobserved: {e.Exception.GetBaseException()}");
        // A step of an update in progress (SelfUpdate), before anything else: the staged copy only swaps itself in
        // and exits, with no window; the installed exe after it goes on as the player, with the link it was handed.
        if (SelfUpdate.Continue(ref args) is { } stepExit)
            return stepExit;
        var settings = PlayerSettings.Load();
        // --start-minimised: start out of sight, as Ctrl+H leaves the window, for Win+/ to bring up (a login item).
        var startHidden = args.Any(IsStartMinimised);
        args = [.. args.Where(a => !IsStartMinimised(a))];
        var input = args.Length > 0 ? string.Join(' ', args) : null;
        // One player: a later launch hands what it was given (a video, a search, or nothing) to the running one,
        // which comes forward with it, and exits before it has a window. --start-minimised (a login item) finds one
        // already running and leaves it as it is. A hand-off that fails opens a window here instead.
        var channel = InstanceGate.ChannelFor("ytplayer");
        using var gate = InstanceGate.TryClaim(channel);
        if (gate is null && (startHidden || InstanceGate.TryHandOff(channel, input ?? "", TimeSpan.FromSeconds(2))))
            return 0;
        // The player's start and its end (after loop.Run, with why) bracket a run in the log: a start with no end
        // after it is a player that died without a word, a crash the handler above could not see or a kill.
        PlayerLog.Write($"started {UpdateCheck.CurrentVersion} (pid {Environment.ProcessId})");
        // Before the renderer's first line: its warnings (a GPU frame over budget, a stuck fence, a lost device) and
        // the GPU it chose go into the log too, so a hang after them is not the first the log hears of it.
        SdlVulkanLog.Logger = new RendererLog(SdlVulkanLog.Logger, PlayerLog.Write);
        // Before the first window, which takes the process's app id when it is made: Windows names the player's
        // media session (and its taskbar button) after the Start menu shortcut that carries the same id, written here
        // the first time (Windows only; AppShell's).
        var shortcut = AppIdentity.Claim(AppId, "YtPlayer");
        if (shortcut is not (StartMenuShortcut.Present or StartMenuShortcut.None))
            PlayerLog.Write($"the Start menu shortcut: {shortcut}");
        var startUrl = YouTubeUrl.ToStartUrl(input);
        // Back from updating itself (SelfUpdate), the player takes up where the old version left off: the video it
        // handed over, and what its settings do not keep (paused, folded).
        var resume = SelfUpdate.Resumed ? ResumeState.Take() : null;
        // A video asked for on the command line opens compact; otherwise the last mode is kept. The video an update
        // hands over is no ask: compact stays as it was.
        var compact = SelfUpdate.Resumed
            ? settings.Compact
            : YouTubeUrl.TryGetVideoId(input ?? "", out _, out _) || (input is null && settings.Compact);
        var onTop = settings.OnTop;
        // The latest release the last update check found (only one of ours: a settings file is no place to take an
        // address to open from). Asked again at start once it is a day old, and whenever the help panel opens.
        var latest = settings is { LatestVersion: { } lv, LatestUrl: { } lu }
                     && lu.StartsWith(UpdateCheck.ReleasesPage, StringComparison.Ordinal)
            ? new Release(lv, lu)
            : null;
        var updateCheckedAt = settings.UpdateCheckedAt;
        var checkingUpdate = false;
        var updateFailed = false;
        // A release build updates itself from the help panel (SelfUpdate); any other only links to the release.
        var installed = SelfUpdate.Installed();
        var updating = false;
        var updateBroke = false;
        bool UpdateAvailable() => latest is { } release && UpdateCheck.IsNewer(UpdateCheck.CurrentVersion, release.Version);

        using var app = SdlVulkanApp.Create();
        // SDL drops the click that activates a window by default, so on an inactive window the first
        // click on a strip button (close, back) only activated it. A floating player should act at once.
        SetHint(Hints.MouseFocusClickthrough, "1");
        // Compact shows from the start only for a video asked for on the command line. Anything else
        // opens a page to browse; from then on the page says when compact comes on screen or leaves it.
        var showing = compact && YouTubeUrl.TryGetVideoId(input ?? "", out _, out _);
        // Two sizes, each remembered: the picture in picture while a compact video shows, and a bigger
        // one to browse in. Unset, they are the design defaults at the scale of the display the window is on,
        // which only the window knows: it opens at the default unscaled, and is sized once it is placed.
        (int W, int H)? pipSaved = settings is { PipWidth: { } pw, PipHeight: { } ph } ? (pw, ph) : null;
        (int W, int H)? browseSaved = settings is { BrowseWidth: { } bw, BrowseHeight: { } bh } ? (bw, bh) : null;
        var opening = (showing ? pipSaved : browseSaved) ?? (showing ? WindowFit.PipDefault : WindowFit.BrowseDefault);

        var window = app.CreateWindow("YtPlayer", opening.W, opening.H,
            maximized: false, borderless: true, alwaysOnTop: onTop, hidden: startHidden);
        if (settings is { X: { } x, Y: { } y })
            window.SetPosition(x, y);
        var pipSize = pipSaved ?? InWindow(window, WindowFit.PipDefault);
        var browseSize = browseSaved ?? InWindow(window, WindowFit.BrowseDefault);
        var startSize = showing ? pipSize : browseSize;
        Fit(window, startSize, showing);
        // The strip is a caption to Windows (the hit test says so), and a double-click on a caption
        // maximizes: a picture-in-picture window has no use for that. The double-click folds it instead.
        // The choice holds when SDL rebuilds the style (a restore from minimized; 7.67): before, Win+D and back
        // gave the box back, and a double-click then maximized the window and folded it at once.
        window.SetMaximizable(false);
        // Win+/ from whichever app is active brings the player forward (Windows only for now). Without it a
        // hidden window could not come back, so then Ctrl+H and --start-minimised only minimize it.
        var summonable = window.TryRegisterGlobalHotKey(SummonHotKey, Keymod.GUI, Keycode.Slash);
        if (!summonable && OperatingSystem.IsWindows())
            PlayerLog.Write("Win+/ is not available: another app holds it");
        if (startHidden && !summonable)
        {
            window.Show();
            window.Minimize();
        }
        // The window's icon says whether the video plays. Windows starts from the exe's own (ApplicationIcon);
        // X11 has only what the window is given.
        var idleIcon = LoadIcon("ytplayer-64.rgba");
        var playingIcon = LoadIcon("ytplayer-playing-64.rgba");
        if (!OperatingSystem.IsWindows() && idleIcon is not null)
            window.SetIcon(idleIcon);

        // Collapsed to the strip alone (the collapse button), and the size it unfolds back to.
        var collapsed = false;
        var expanded = startSize;
        (int W, int H) Size()
        {
            GetWindowSize(window.Handle, out var w, out var h);
            return collapsed ? expanded : (w, h);
        }

        // Compact came on screen or left it: keep the height the window is leaving and take the other
        // mode's, with the width it has now, so it stays in place (WindowFit.Switch). Collapsed, only the
        // size it will unfold to changes; the window stays a strip.
        int? narrowPipWidth = null;
        // Fullscreen (YouTube's, see SetFullscreen) the window is the display's size, which is neither mode's: a
        // switch waits until it ends.
        var fullscreen = false;
        bool? showingAfterFullscreen = null;
        void ShowCompact(bool on)
        {
            if (fullscreen)
            {
                showingAfterFullscreen = on;
                return;
            }
            if (on == showing)
                return;
            var (w, h) = Size();
            if (showing)
                pipSize = (w, h);
            else
                browseSize = (w, h);
            showing = on;
            var browseMinimumWidth = InWindow(window, WindowFit.BrowseMinimum).W;
            (var size, narrowPipWidth) = WindowFit.Switch(
                on, w, (on ? pipSize : browseSize).Item2, browseMinimumWidth, narrowPipWidth);
            if (collapsed)
            {
                expanded = size;
                return;
            }
            Fit(window, size, on);
        }

        window.GetSizeInPixels(out var pixelW, out var pixelH);
        var context = VulkanContext.CreateForSharedDevice(app.Device, window.Surface, (uint)pixelW, (uint)pixelH);
        var renderer = new VkRenderer(context, (uint)pixelW, (uint)pixelH);
        // The strip and border: a layout tree in design units, which the display's scale maps to pixels. The scale
        // is set again on every resize, as a window moved to another display is resized to its scale.
        // Its fullscreen button is YouTube's fullscreen, which only WebView2 takes from the strip (through the DevTools
        // protocol) and reports for the window to follow (FullscreenChanged).
        var chrome = new PipChrome<VulkanContext>(renderer)
        {
            FontPath = TitleFont() ?? "", DpiScale = window.DisplayScale, CanFullscreen = OperatingSystem.IsWindows(),
            CanSummon = summonable,
        };

        // Window points are pixels on Windows and X11, the platforms with a web view backend, so SDL's hit test
        // asks the regions the last paint drew what is at its point.
        _hitTest = (nint _, in Point area, nint _) => chrome.FrameAreaAt(area.X, area.Y);
        SetWindowHitTest(window.Handle, _hitTest, nint.Zero);

        // The window's entry in the system's media controls (Windows' media flyout, the lock screen, the media keys),
        // under the player's name: the track the page's media session has, play or pause as the video does, and the
        // presses back to the page. WebView2 would put the page's media there too, as "Unknown app", so it is told not
        // to while the window has its own (Windows only for now).
        var media = window.TryGetMediaControls();
        var web = NativeWebView.Create(new NativeWebViewOptions
        {
            UserDataFolder = PlayerSettings.WebProfileFolder,
            SystemMediaControls = media is null,
        });
        // What the host tells the page goes through this, which sends nothing until the page has been heard from and
        // drops what the web view refuses: a web view throws on a message it is not ready for, and a strip button
        // pressed then took the player down.
        var page = new PageLink(web, PlayerLog.Write);
        // The strip shows the page's title, and play or pause as the page's video does.
        string? stripTitle = null;
        var playing = false;
        // The strip and border light up while the window is the active one (read in OnLoopIteration).
        var active = false;
        ChromeState State() => new(fullscreen ? PipMode.Fullscreen : collapsed ? PipMode.Collapsed : PipMode.Normal,
            active, compact, showing, onTop, playing, stripTitle, UpdateAvailable());
        // The page goes where the chrome's tree puts it, in pixels. Collapsed, it is hidden, with nowhere to go.
        void PlaceWebView()
        {
            if (collapsed)
                return;
            window.GetSizeInPixels(out var w, out var h);
            web.SetBounds(chrome.WebViewBounds(State(), w, h));
        }
        var adLog = new AdLog();
        // Why the loop stopped, for the log: the player's own quits say so; anything else is a quit from outside or
        // the GPU given up on (OnQuit, OnGpuWedged).
        string? stopReason = null;
        // The page has said {ready}: WebView2 takes no message before its first navigation, and throws. What the host
        // tells the page unasked (the update check's answer, which can come back first) waits for it.
        var pageReady = false;
        void TellPage(string json)
        {
            if (pageReady)
                page.Post(json);
        }
        // The document in the web view has said it can draw the strip's tooltips ({tips}), under the strip where the
        // host cannot; it says so again when it goes. Until it has, the strip draws them itself.
        var pageTips = false;
        // Running from the first page being asked for until the page is heard from. A web view that has not come up
        // a quarter of a minute on is not coming (one started while the last player's browser was still closing has
        // stayed grey): the log and the strip then say so, once.
        Stopwatch? pageAsked = null;
        // Started hidden (at sign-in, --start-minimised) with Win+/ to bring it back: the page waits for the window
        // to be first shown. A loaded YouTube page costs some 300 MB in its renderer, and more on the GPU, for a
        // window nobody may open for hours; with nothing loaded the web view costs little.
        string? deferredUrl = null;
        // A link opened while the page is up (a pasted one, a later launch's): a video plays within the page, which
        // keeps YouTube's queue (a full load drops it) and, in use, puts the video in it; anything else loads.
        void OpenLink(string url)
        {
            deferredUrl = null;
            if (pageReady && YouTubeUrl.TryGetVideoId(url, out _, out _))
            {
                page.Post(CompactMessage(compact));
                page.Post(PlayMessage(url));
            }
            else
                web.Navigate(url);
        }
        // Not awaited: registered before the attach, it completes only after it, and the first
        // navigation waits for it anyway. Watched only so a refusal is not silent.
        web.AddScriptAtDocumentStartAsync(LoadTweaksScript()).ContinueWith(
            t => PlayerLog.Write($"the page script was not registered: {t.Exception?.GetBaseException().Message}"),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        web.MessageReceived += json => UiQueue.Enqueue(() =>
        {
            page.Heard();
            var message = PageMessage.Parse(json);
            // The page asks for the compact state when it is ready (every full load), since a
            // document-start script cannot know it. A new document is never fullscreen, and leaving the old
            // one that was (a pasted link, a launch's hand-off) says nothing, so the window leaves it here.
            if (message.Ready)
            {
                pageReady = true;
                // Paused when the old version updated: the page pauses the video once it starts (the first page only).
                if (resume is { Paused: true })
                    page.Post("""{"startPaused":true}""");
                resume = resume is null ? null : resume with { Paused = false };
                SetFullscreen(false);
                page.Post(CompactMessage(compact));
            }
            if (message.Tips is { } tips)
                pageTips = tips;
            if (message.Showing is { } on)
                ShowCompact(on);
            // Ctrl+V outside a text field: a YouTube link opens; anything else on the clipboard is left alone.
            if (message.Paste is { } pasted && YouTubeUrl.FromPastedLink(pasted) is { } pastedUrl)
                OpenLink(pastedUrl);
            if (message.Copy is { } link)
                SetClipboardText(link);
            if (message.Ad is { } ad && (ad ? adLog.Started(System.DateTime.Now) : adLog.Ended(System.DateTime.Now)) is { } adLine)
                PlayerLog.Write(adLine);
            if (message.SkipAt is { } skip)
            {
                PlayerLog.Write(adLog.SkipOffered(System.DateTime.Now, skip.X, skip.Y, canClick: OperatingSystem.IsWindows()));
                ClickInPage(page, skip.X, skip.Y);
            }
            if (message.Playing is { } p && p != playing)
            {
                playing = p;
                if ((p ? playingIcon : idleIcon) is { } icon)
                    window.SetIcon(icon);
                media?.SetPlaying(p);
            }
            if (message.Media is { } track)
            {
                media?.SetTrack(track.Title, track.Artist, track.Artwork);
                media?.SetSkipEnabled(track.Previous, track.Next);
            }
            if (message.MediaGone)
                media?.Clear();
            if (message.Hide)
                Hide();
            if (message.Collapse is { } fold)
                SetCollapsed(fold);
            if (message.QueueClipboard)
                QueueFromClipboard(page);
            if (message.Help)
                ShowHelp();
            // Ctrl+S: the transcript the page read, written into the Downloads folder; the page says how it went.
            if (message.Transcript is { } transcript)
                page.Post(SaveTranscript(transcript));
            // Only the release the check found, whatever the page asks.
            if (message.OpenRelease && UpdateAvailable() && !OpenURL(latest!.Url))
                PlayerLog.Write($"the release page did not open: {GetError()}");
            if (message.Update)
                StartUpdate(YouTubeUrl.FromPastedLink(message.Resume), message.ResumePaused);
        });
        web.TitleChanged += title => UiQueue.Enqueue(() =>
        {
            stripTitle = TitleFor(title);
            window.SetTitle(stripTitle);
        });
        web.PageError += error => Console.Error.WriteLine($"[page] {error}");
        web.AttachToWindow(window);
        PlaceWebView();
        if (startHidden && summonable)
            deferredUrl = startUrl;
        else
        {
            web.Navigate(startUrl);
            pageAsked = Stopwatch.StartNew();
        }

        // Whether a newer release is out, from GitHub: the help button wears a red dot while one is, and the help
        // panel says which. The answer goes to the page too, which shows it if the panel is open.
        void CheckForUpdate()
        {
            if (checkingUpdate)
                return;
            checkingUpdate = true;
            UpdateCheck.LatestAsync().ContinueWith(t => UiQueue.Enqueue(() =>
            {
                checkingUpdate = false;
                var found = t.IsCompletedSuccessfully ? t.Result : null;
                updateFailed = found is null;
                if (found is not null)
                {
                    latest = found;
                    updateCheckedAt = DateTimeOffset.Now;
                }
                TellPage(HelpNow(open: false));
            }), TaskScheduler.Default);
        }
        if (updateCheckedAt is not { } checkedAt || DateTimeOffset.Now - checkedAt > UpdateCheck.Fresh)
            CheckForUpdate();

        // The strip's help button and F1: the help panel, in the page (the keys, the version, and whether a newer one
        // is out), or closed if it is open. Folded, the window unfolds to show it.
        void ShowHelp()
        {
            SetCollapsed(false);
            CheckForUpdate();
            TellPage(HelpNow(open: true));
        }
        string HelpNow(bool open)
            => HelpMessage(open, UpdateCheck.CurrentVersion,
                UpdateAvailable() ? latest!.Version : null,
                updating ? "downloading"
                : updateBroke ? "failed"
                : UpdateAvailable() ? "newer" : checkingUpdate ? "checking" : updateFailed ? "unknown" : "current",
                summonable, canUpdate: installed is not null);

        // The help panel's update button: the newer release downloaded and checked into update\ beside the exe, then
        // handed over to (SelfUpdate). The window goes out of sight at once and the player quits as usual; the next
        // window is the new version's, with what was playing. Until the download is done, the player plays on.
        void StartUpdate(string? link, bool paused)
        {
            if (installed is null || updating)
                return;
            updating = true;
            updateBroke = false;
            TellPage(HelpNow(open: false));
            SelfUpdate.StageAsync(installed).ContinueWith(t => UiQueue.Enqueue(() =>
            {
                updating = false;
                var staged = t.IsCompletedSuccessfully ? t.Result : null;
                // What the new version takes up that the link and the settings do not carry.
                if (staged is { Exe: not null })
                    new ResumeState { Paused = paused, Collapsed = collapsed }.Save();
                if (staged is not null && SelfUpdate.StartStaged(staged, link))
                {
                    window.Hide();
                    stopReason = $"to update to {staged.Version}";
                    PushQuit();
                    return;
                }
                updateBroke = true;
                TellPage(HelpNow(open: false));
            }), TaskScheduler.Default);
        }

        // Ctrl+H and the strip's hide button: out of sight, off the taskbar and Alt+Tab too, the video playing on;
        // Win+/ brings it back, as does launching it again. With no Win+/ to do that, it is only minimized.
        void Hide()
        {
            if (summonable)
                window.Hide();
            else
                window.Minimize();
        }

        // YouTube's fullscreen (F, its button, a double-click on the video) would fill only the web view, which is
        // the small window: while it lasts the window fills its display, with no strip or border, and leaving it
        // (Esc, F) puts the window back where and as big as it was. (SDL rebuilds the window's style on the way
        // out, maximize box included; SdlVulkan.Renderer 7.67 takes the box away again, as SetMaximizable asked.)
        void SetFullscreen(bool on)
        {
            if (on == fullscreen || collapsed)
                return;
            fullscreen = on;
            SetWindowFullscreen(window.Handle, on);
            PlaceWebView();
            if (!on && showingAfterFullscreen is { } pending)
            {
                showingAfterFullscreen = null;
                ShowCompact(pending);
            }
        }

        // The web view says when the page goes fullscreen or leaves it, and the window follows within the event,
        // which WebView2 raises on this thread. The Linux backend does not raise it yet, so there the page fills
        // the view.
        web.FullscreenChanged += full =>
        {
            if (OperatingSystem.IsWindows())
                SetFullscreen(full);
            else
                UiQueue.Enqueue(() => SetFullscreen(full));
        };

        // The collapse button: fold the window to its strip, the video playing on behind it, and unfold
        // it to the size it had. The corner nearest the screen's corner stays put either way.
        void SetCollapsed(bool on)
        {
            // Fullscreen, there is no strip to fold to.
            if (on == collapsed || fullscreen)
                return;
            GetWindowSize(window.Handle, out var w, out var h);
            collapsed = on;
            if (on)
            {
                expanded = (w, h);
                // Hidden rather than sized to nothing: an X11 window cannot be 0 pixels tall.
                web.SetVisible(false);
                // The mode's minimum width, and the strip with the border under it, both in design units.
                var (minimumWidth, collapsedHeight) = window.ToWindowSize(
                    (showing ? WindowFit.PipMinimum : WindowFit.BrowseMinimum).W, chrome.CollapsedHeight());
                window.ResizeInPlace(w, collapsedHeight, minimumWidth, collapsedHeight);
            }
            else
            {
                Fit(window, expanded, showing);
                web.SetVisible(true);
            }
            PlaceWebView();
        }

        var loop = new SdlEventLoop(window, renderer)
        {
            BackgroundColor = PipChrome<VulkanContext>.Background,
            OnResize = (_, _) =>
            {
                chrome.DpiScale = window.DisplayScale;
                PlaceWebView();
            },
            // Keys reach here only while the strip has focus (after a click on it); in the page, the
            // tweaks script handles the same shortcuts.
            OnKeyDown = key =>
            {
                switch (key.Modifiers, key.Key)
                {
                    case (InputModifier.Ctrl, InputKey.V):
                        if (YouTubeUrl.FromPastedLink(GetClipboardText()) is { } clipboardUrl)
                            OpenLink(clipboardUrl);
                        return true;
                    case (InputModifier.Ctrl, InputKey.F):
                        web.Focus();
                        page.Post("""{"search":true}""");
                        return true;
                    // The page knows the video and the time; it answers with a copy message.
                    case (InputModifier.Ctrl, InputKey.C):
                        page.Post("""{"copyLink":"plain"}""");
                        return true;
                    case (InputModifier.Ctrl | InputModifier.Shift, InputKey.C):
                        page.Post("""{"copyLink":"time"}""");
                        return true;
                    case (InputModifier.Ctrl, InputKey.P):
                        page.Post("""{"togglePlay":"key"}""");
                        return true;
                    case (InputModifier.Ctrl, InputKey.H):
                        Hide();
                        return true;
                    // Fold and unfold, as the chevron does; the page sends the same keys as {collapse}.
                    case (InputModifier.Ctrl, InputKey.Up):
                        SetCollapsed(true);
                        return true;
                    case (InputModifier.Ctrl, InputKey.Down):
                        SetCollapsed(false);
                        return true;
                    // The queue: the strip has no pointer over a video, so Ctrl+Q queues the clipboard's link; Ctrl+L
                    // unfolds the window to show the list, which is the page's.
                    case (InputModifier.Ctrl, InputKey.Q):
                        QueueFromClipboard(page);
                        return true;
                    case (InputModifier.Ctrl, InputKey.L):
                        SetCollapsed(false);
                        web.Focus();
                        page.Post("""{"listQueue":true}""");
                        return true;
                    // The video's transcript, which the page reads and sends back for the host to write; its
                    // notices are the page's, so the window unfolds to show them.
                    case (InputModifier.Ctrl, InputKey.S):
                        SetCollapsed(false);
                        web.Focus();
                        page.Post("""{"saveTranscript":true}""");
                        return true;
                    // YouTube's own keys for the next and previous videos, which the page takes itself; on the strip
                    // they do what its buttons do.
                    case (InputModifier.Shift, InputKey.N):
                        page.Post("""{"next":true}""");
                        return true;
                    case (InputModifier.Shift, InputKey.P):
                        page.Post("""{"previous":true}""");
                        return true;
                    case (InputModifier.None, InputKey.F1):
                        ShowHelp();
                        web.Focus();
                        return true;
                    default:
                        return false;
                }
            },
        };

        // The strip's presses and hover go through DIR.Lib's router, which finds them in the regions the chrome
        // painted. Only the left button presses one.
        IPixelWidget[] widgets = [chrome];
        var router = new InputRouter(chrome.Ui, new BackgroundTaskTracker(), loop.RequestRedraw) { Widgets = () => widgets };
        loop.OnPointerInput = evt => evt is not InputEvent.MouseDown { Button: not MouseButton.Left } && router.Handle(evt);
        // The tooltip of the button the pointer has rested on, once the router's delay has run out. It belongs under
        // the button, which is the page's to draw ({tip}: the web view covers everything under the strip). Folded,
        // or with a document that has not said it can (still loading, an error page), the strip draws it itself,
        // beside the button.
        string? tipSent = null;
        loop.OnRender = () =>
        {
            var due = router.Tooltip;
            var inPage = pageTips && !collapsed && !fullscreen;
            string? tip = null;
            if (inPage && due is { } d)
            {
                window.GetSizeInPixels(out var w, out var h);
                tip = TipMessage(d.Text, d.Anchor.X + d.Anchor.Width / 2 - chrome.WebViewBounds(State(), w, h).UpperLeft.X);
            }
            if (tip != tipSent)
            {
                tipSent = tip;
                page.Post(tip ?? """{"tip":null}""");
            }
            chrome.Render(State(), inPage ? null : due);
            router.AfterPaint();
        };
        chrome.Pressed = button =>
        {
            switch (button)
            {
                case PipButton.Back:
                    page.Run("history.back()");
                    break;
                case PipButton.Home:
                    // With no page to tell (the web view not up yet, or never), a load of it.
                    if (!page.Post("""{"home":true}"""))
                        web.Navigate(YouTubeUrl.Home);
                    break;
                case PipButton.Previous:
                    page.Post("""{"previous":true}""");
                    break;
                case PipButton.PlayPause:
                    page.Post("""{"togglePlay":true}""");
                    break;
                case PipButton.Next:
                    page.Post("""{"next":true}""");
                    break;
                case PipButton.Collapse:
                    SetCollapsed(!collapsed);
                    break;
                case PipButton.Compact:
                    compact = !compact;
                    page.Post(CompactMessage(compact));
                    break;
                case PipButton.Pin:
                    onTop = !onTop;
                    window.SetAlwaysOnTop(onTop);
                    break;
                case PipButton.Help:
                    ShowHelp();
                    break;
                case PipButton.Hide:
                    Hide();
                    return;
                case PipButton.Fullscreen:
                    // YouTube's own fullscreen, which the window follows (FullscreenChanged). A page goes fullscreen
                    // only on the user's gesture, and the press was on the strip, not in the page: the page script
                    // clicks YouTube's button on an event the DevTools protocol dispatches as the user's. Folded, the
                    // window unfolds first: there is no fullscreen from the strip alone. And the page takes the keyboard
                    // before it is asked, not after: since WebView2 157 a page that is not the focused one is refused
                    // fullscreen, whatever the gesture, and the press left the keyboard on the strip.
                    SetCollapsed(false);
                    web.Focus();
                    page.Protocol("Runtime.evaluate",
                        """{"expression":"window.dispatchEvent(new Event('ytpip-fullscreen'))","userGesture":true}""");
                    break;
                case PipButton.Close:
                    stopReason = "the strip's close button";
                    PushQuit();
                    return;
            }
            // A click on the strip leaves the keyboard on the strip; hand it back to the page, so
            // the next key (space, Ctrl+F) reaches the video. Collapsed, there is no page to give it to.
            if (!collapsed)
                web.Focus();
        };
        // Win+/, or a later launch: the player comes forward, hidden, minimized or behind others, with the keyboard
        // (the page's, unless it is folded to its strip). Activate (AppShell's) restores only a minimized window.
        void Summon()
        {
            if (window.IsHidden)
                window.Show();
            if (deferredUrl is { } url)
            {
                deferredUrl = null;
                web.Navigate(url);
                pageAsked ??= Stopwatch.StartNew();
            }
            window.Activate();
            if (!collapsed)
                web.Focus();
            loop.RequestRedraw();
        }

        // A later launch's video or search opens here, as it would have there: a video compact, and the window
        // unfolded to show it. Given nothing, the player only comes forward.
        void OpenHandedOff(string handed)
        {
            if (!string.IsNullOrWhiteSpace(handed))
            {
                if (YouTubeUrl.TryGetVideoId(handed, out _, out _))
                    compact = true;
                SetCollapsed(false);
                OpenLink(YouTubeUrl.ToStartUrl(handed));
            }
            Summon();
        }

        // The strip and border light up while the window is the active one, so it is plain when keys go
        // to it. Nothing signals a change, so it is read every iteration (at least every 16 ms).
        loop.OnLoopIteration = () =>
        {
            var any = false;
            while (UiQueue.TryDequeue(out var action))
            {
                action();
                any = true;
            }
            // A later launch's hand-off, queued by the gate's own thread. Read here, as this runs while the window
            // is hidden or minimized too, when nothing renders.
            while (gate is not null && gate.TryDequeue(out var handoff))
            {
                OpenHandedOff(handoff.Payload);
                any = true;
            }
            if (window.IsActive != active)
            {
                active = !active;
                // However the window was activated (a click on the strip or border, the taskbar,
                // Alt+Tab), the keys are for the page, not the strip.
                if (active && !collapsed)
                    web.Focus();
                any = true;
            }
            if (pageAsked is { } asked && (page.Up || asked.Elapsed > TimeSpan.FromSeconds(15)))
            {
                pageAsked = null;
                if (!page.Up)
                {
                    PlayerLog.Write("the web view has not come up, 15 s after its first page was asked for");
                    stripTitle = "No page came up: close YtPlayer and start it again";
                    window.SetTitle(stripTitle);
                    any = true;
                }
            }
            // A tooltip came due while the pointer rested: nothing else asks for the frame that shows it.
            if (any || router.TooltipNeedsFrame)
                loop.RequestRedraw();
        };

        // Win+/ from whichever app is active. The press is what lets a window in the background take the foreground.
        if (summonable)
            loop.OnGlobalHotKey = id =>
            {
                if (id == SummonHotKey)
                    Summon();
            };

        // A double-click on the strip folds the window, as the collapse button does (Windows only for now: SDL
        // gives the app no click on a caption, and Windows has one on its hook).
        if (window.TryReportCaptionDoubleClicks())
            loop.OnCaptionDoubleClick = () => SetCollapsed(!collapsed);

        // The media flyout's buttons and the media keys: the page does what the browser would have done with them,
        // YouTube's own handlers first.
        if (media is not null)
            loop.OnMediaButton = button => page.Post(MediaActionMessage(button));

        // Folded when the old version updated: folded again, the page loading behind the strip.
        if (resume is { Collapsed: true })
            SetCollapsed(true);

        // A quit the player did not ask for: Alt+F4, the taskbar's close, Windows signing out or shutting down.
        loop.OnQuit = () =>
        {
            stopReason ??= "asked to quit from outside (Alt+F4, the taskbar, Windows)";
            return false;
        };
        // The GPU stopped answering and the renderer gave up on it, which stops the loop as a quit does.
        loop.OnGpuWedged = () => stopReason = "the GPU stopped responding (the renderer gave up on it)";
        loop.OnRenderDegraded = () => PlayerLog.Write("the GPU had to recover several times in quick succession");

        loop.Run();
        PlayerLog.Write($"exiting: {stopReason ?? "the event loop stopped, for no reason it gave"}");
        // How long the way out takes (the settings, the web view, the GPU), for a close that felt slow: an
        // "exiting" with no "stopped" after it is a player that hung or died on the way out.
        var stopping = Stopwatch.StartNew();

        // Fullscreen, the window's place and size are the display's; the ones to remember are those it goes back to.
        SetFullscreen(false);
        window.GetPosition(out var px, out var py);
        var (exitW, exitH) = Size();
        if (showing)
            pipSize = (exitW, exitH);
        else
            browseSize = (exitW, exitH);
        new PlayerSettings
        {
            X = px, Y = py,
            PipWidth = pipSize.Item1, PipHeight = pipSize.Item2,
            BrowseWidth = browseSize.Item1, BrowseHeight = browseSize.Item2,
            OnTop = onTop, Compact = compact,
            LatestVersion = latest?.Version, LatestUrl = latest?.Url, UpdateCheckedAt = updateCheckedAt,
        }.Save();

        web.Dispose();
        renderer.Dispose();
        context.Dispose();
        window.Dispose();
        PlayerLog.Write($"stopped after {stopping.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }

    // A left click at (x, y) in the page's CSS pixels that the page takes as the user's own, which a page
    // script's click() is not: YouTube's ad Skip button ignores those. Sent as the DevTools protocol's
    // input events, all three from here, the UI thread, as WebView2 requires; they arrive in order.
    // WebKitGTK has no such protocol, so on Linux an ad's Skip waits for the viewer.
    private static void ClickInPage(PageLink page, double x, double y)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var at = $"\"x\":{x.ToString(CultureInfo.InvariantCulture)},\"y\":{y.ToString(CultureInfo.InvariantCulture)}";
        // A step the page does not take is logged by the link (once), and the next ones go no further.
        _ = page.Protocol("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",{at}}}")
            && page.Protocol("Input.dispatchMouseEvent", $"{{\"type\":\"mousePressed\",{at},\"button\":\"left\",\"clickCount\":1}}")
            && page.Protocol("Input.dispatchMouseEvent", $"{{\"type\":\"mouseReleased\",{at},\"button\":\"left\",\"clickCount\":1}}");
    }

    // Ctrl+Q with no video under the pointer: the video whose link is on the clipboard goes to the page's queue
    // (null tells the page there was none). A link the way Ctrl+V takes one, a bare id not.
    private static void QueueFromClipboard(PageLink page)
    {
        var id = YouTubeUrl.VideoIdFromPastedLink(GetClipboardText());
        page.Post(id is null ? """{"queue":null}""" : $$"""{"queue":"{{id}}"}""");
    }

    // Ctrl+S: the transcript goes into the Downloads folder as a text file, and the answer to the page says where
    // ({transcriptSaved}).
    private static string SaveTranscript(PageTranscript transcript)
    {
        var folder = DownloadsFolder();
        return TranscriptSavedMessage(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), transcript.TrySave(folder));
    }

    // The user's Downloads folder, wherever they keep it (SDL asks the system); failing that, the usual place.
    private static string DownloadsFolder()
        => GetUserFolder(Folder.Downloads) is { Length: > 0 } folder
            ? folder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // {tip}: the page draws a strip button's tooltip at its top, under the button: x is the button's middle, in pixels
    // from the page's left edge.
    internal static string TipMessage(string text, float x)
    {
        var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("tip");
            json.WriteString("text", text);
            json.WriteNumber("x", MathF.Round(x));
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // {transcriptSaved}: the file the transcript was written to and the folder it is in, by name, for the page's
    // notice; null when it was not written.
    internal static string TranscriptSavedMessage(string folder, string? file)
    {
        if (file is null)
            return """{"transcriptSaved":null}""";
        var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("transcriptSaved");
            json.WriteString("folder", folder);
            json.WriteString("file", file);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // {play}: the page opens the video of a watch URL within itself (a pasted link, a later launch's).
    internal static string PlayMessage(string watchUrl) => $$"""{"play":"{{JsonEncodedText.Encode(watchUrl)}}"}""";

    // {help}: open (or close) the panel, or, with open false, bring an open one up to date. newer is the release that
    // is out, update what the panel says of it (newer, checking, current, unknown, downloading, failed), summon
    // whether Win+/ is ours, canUpdate whether this build updates itself (a release build).
    internal static string HelpMessage(bool open, string version, string? newer, string update, bool summon,
        bool canUpdate = false)
    {
        var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("help");
            json.WriteBoolean("open", open);
            json.WriteString("version", version);
            if (newer is null)
                json.WriteNull("newer");
            else
                json.WriteString("newer", newer);
            json.WriteString("update", update);
            json.WriteBoolean("summon", summon);
            json.WriteBoolean("canUpdate", canUpdate);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static string CompactMessage(bool compact) => compact ? """{"compact":true}""" : """{"compact":false}""";

    // A press of the media controls, as the media session action the browser would have sent the page.
    internal static string MediaActionMessage(MediaButton button) => button switch
    {
        MediaButton.Play => """{"mediaAction":"play"}""",
        MediaButton.Pause => """{"mediaAction":"pause"}""",
        MediaButton.Stop => """{"mediaAction":"stop"}""",
        MediaButton.Next => """{"mediaAction":"nexttrack"}""",
        _ => """{"mediaAction":"previoustrack"}""",
    };

    private static string TitleFor(string pageTitle)
    {
        const string suffix = " - YouTube";
        var title = pageTitle.EndsWith(suffix, StringComparison.Ordinal) ? pageTitle[..^suffix.Length] : pageTitle;
        return string.IsNullOrWhiteSpace(title) ? "YtPlayer" : title;
    }

    // A font for the strip's title: the platform's own face. Null, if there is none to be had, leaves the
    // title out rather than failing the start.
    private static string? TitleFont()
    {
        try
        {
            return FontResolver.ResolveSystemFont() is { Length: > 0 } path && File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    // Sets the mode's minimum, then sizes and places the window in place of current, on its display.
    // Sizes the window where it stands on its display (SdlVulkanWindow.ResizeInPlace), never under the mode's minimum.
    private static void Fit(SdlVulkanWindow window, (int W, int H) size, bool pip)
    {
        var min = InWindow(window, pip ? WindowFit.PipMinimum : WindowFit.BrowseMinimum);
        window.ResizeInPlace(size.W, size.H, min.W, min.H);
    }

    // One of WindowFit's sizes, which are design units, in the window's coordinates.
    private static (int W, int H) InWindow(SdlVulkanWindow window, (int W, int H) design)
        => window.ToWindowSize(design.W, design.H);

    // A 64x64 window icon from the build's resources (tools/make-icon.py draws them). Windows takes the
    // window's first icon from the exe's (ApplicationIcon), at every size it needs; X11 has only what the
    // window is given.
    private static RgbaImage? LoadIcon(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
            return null;
        var icon = new RgbaImage(64, 64);
        stream.ReadExactly(icon.Pixels);
        return icon;
    }

    private static bool IsStartMinimised(string arg)
        => arg.Equals("--start-minimised", StringComparison.OrdinalIgnoreCase)
           || arg.Equals("--start-minimized", StringComparison.OrdinalIgnoreCase);


    private static void PushQuit()
    {
        var quit = new Event { Type = (uint)EventType.Quit };
        PushEvent(ref quit);
    }

    private static string LoadTweaksScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("yt-tweaks.js")
            ?? throw new InvalidOperationException("The yt-tweaks.js resource is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
