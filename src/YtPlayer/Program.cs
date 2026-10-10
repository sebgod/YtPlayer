using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using DIR.Lib;
using SdlVulkan.Renderer;
using SdlVulkan.Renderer.WebView;
using static SDL3.SDL;

namespace YtPlayer;

/// <summary>
/// A picture-in-picture YouTube player: a small borderless window that stays on top, showing YouTube
/// in the platform's web view (WebView2 on Windows, WebKitGTK on Linux) under a thin strip that drags
/// the window and holds four buttons: back, compact (the video alone), stay on top, close.
/// </summary>
/// <remarks>Usage: <c>ytplayer [video URL | video id | search words]</c>.</remarks>
internal static class Program
{
    private static readonly ConcurrentQueue<Action> UiQueue = new();
    private static PipLayout _layout;
    private static HitTest? _hitTest; // held for as long as SDL may call it

    [STAThread] // WebView2 requires an STA thread
    private static int Main(string[] args)
    {
        var settings = PlayerSettings.Load();
        var input = args.Length > 0 ? string.Join(' ', args) : null;
        var startUrl = YouTubeUrl.ToStartUrl(input);
        // A video asked for on the command line opens compact; otherwise the last mode is kept.
        var compact = YouTubeUrl.TryGetVideoId(input ?? "", out _, out _) || (input is null && settings.Compact);
        var onTop = settings.OnTop;

        using var app = SdlVulkanApp.Create();
        // SDL drops the click that activates a window by default, so on an inactive window the first
        // click on a strip button (close, back) only activated it. A floating player should act at once.
        SetHint(Hints.MouseFocusClickthrough, "1");
        // Two sizes, each remembered: the picture in picture while a compact video shows, and a bigger
        // one to browse in. Unset, they are the logical defaults at the display's scale.
        var startScale = GetDisplayContentScale(GetPrimaryDisplay()) is > 0 and var s ? s : 1f;
        var pipSize = settings is { PipWidth: { } pw, PipHeight: { } ph }
            ? (pw, ph) : WindowFit.Scaled(WindowFit.PipDefault, startScale);
        var browseSize = settings is { BrowseWidth: { } bw, BrowseHeight: { } bh }
            ? (bw, bh) : WindowFit.Scaled(WindowFit.BrowseDefault, startScale);
        // Compact shows from the start only for a video asked for on the command line. Anything else
        // opens a page to browse; from then on the page says when compact comes on screen or leaves it.
        var showing = compact && YouTubeUrl.TryGetVideoId(input ?? "", out _, out _);
        var startSize = showing ? pipSize : browseSize;

        var window = app.CreateWindow("YtPlayer", startSize.Item1, startSize.Item2,
            maximized: false, borderless: true, alwaysOnTop: onTop);
        if (settings is { X: { } x, Y: { } y })
            window.SetPosition(x, y);
        window.GetPosition(out var startX, out var startY);
        Fit(window, new Box(startX, startY, startSize.Item1, startSize.Item2), startSize, showing);
        // The strip is a caption to Windows (the hit test says so), and a double-click on a caption
        // maximizes: a picture-in-picture window has no use for that.
        window.SetMaximizable(false);
        if (!OperatingSystem.IsWindows())
            SetIconFromResource(window);

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
        void ShowCompact(bool on)
        {
            if (on == showing)
                return;
            var (w, h) = Size();
            if (showing)
                pipSize = (w, h);
            else
                browseSize = (w, h);
            showing = on;
            var browseMinimumWidth = WindowFit.Scaled(WindowFit.BrowseMinimum, window.DisplayScale).W;
            (var size, narrowPipWidth) = WindowFit.Switch(
                on, w, (on ? pipSize : browseSize).Item2, browseMinimumWidth, narrowPipWidth);
            if (collapsed)
            {
                expanded = size;
                return;
            }
            window.GetPosition(out var wx, out var wy);
            Fit(window, new Box(wx, wy, w, h), size, on);
        }

        window.GetSizeInPixels(out var pixelW, out var pixelH);
        var context = VulkanContext.CreateForSharedDevice(app.Device, window.Surface, (uint)pixelW, (uint)pixelH);
        var renderer = new VkRenderer(context, (uint)pixelW, (uint)pixelH);
        var chrome = new PipChrome(renderer, TitleFont());
        _layout = CurrentLayout(window, collapsed);

        _hitTest = (nint _, in Point area, nint _) => _layout.HitTest(area.X, area.Y);
        SetWindowHitTest(window.Handle, _hitTest, nint.Zero);

        var web = NativeWebView.Create(new NativeWebViewOptions { UserDataFolder = PlayerSettings.WebProfileFolder });
        // The strip shows the page's title, and play or pause as the page's video does.
        string? stripTitle = null;
        var playing = false;
        // Not awaited: registered before the attach, it completes only after it, and the first
        // navigation waits for it anyway. Watched only so a refusal is not silent.
        web.AddScriptAtDocumentStartAsync(LoadTweaksScript()).ContinueWith(
            t => Console.Error.WriteLine($"[tweaks] not registered: {t.Exception?.GetBaseException().Message}"),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        web.MessageReceived += json => UiQueue.Enqueue(() =>
        {
            var message = PageMessage.Parse(json);
            // The page asks for the compact state when it is ready (every full load), since a
            // document-start script cannot know it.
            if (message.Ready)
                web.PostMessage(CompactMessage(compact));
            if (message.Showing is { } on)
                ShowCompact(on);
            if (message.Paste is { } pasted)
                OpenPastedLink(web, pasted);
            if (message.Copy is { } link)
                SetClipboardText(link);
            if (message.SkipAt is { } skip)
                ClickInPage(web, skip.X, skip.Y);
            if (message.Playing is { } p)
                playing = p;
        });
        web.TitleChanged += title => UiQueue.Enqueue(() =>
        {
            stripTitle = TitleFor(title);
            window.SetTitle(stripTitle);
        });
        web.PageError += error => Console.Error.WriteLine($"[page] {error}");
        web.AttachToWindow(window);
        web.SetBounds(WebViewPixels(_layout, window));
        web.Navigate(startUrl);

        // The collapse button: fold the window to its strip, the video playing on behind it, and unfold
        // it to the size it had. The corner nearest the screen's corner stays put either way.
        void SetCollapsed(bool on)
        {
            if (on == collapsed)
                return;
            GetWindowSize(window.Handle, out var w, out var h);
            window.GetPosition(out var wx, out var wy);
            var current = new Box(wx, wy, w, h);
            collapsed = on;
            if (on)
            {
                expanded = (w, h);
                // Hidden rather than sized to nothing: an X11 window cannot be 0 pixels tall.
                web.SetVisible(false);
                var minimumWidth = WindowFit.Scaled(showing ? WindowFit.PipMinimum : WindowFit.BrowseMinimum,
                    window.DisplayScale).W;
                window.SetMinimumSize(minimumWidth, PipLayout.CollapsedHeight);
                Place(window, current, (w, PipLayout.CollapsedHeight));
            }
            else
            {
                Fit(window, current, expanded, showing);
                web.SetVisible(true);
            }
            _layout = CurrentLayout(window, collapsed);
            if (!collapsed)
                web.SetBounds(WebViewPixels(_layout, window));
        }

        var loop = new SdlEventLoop(window, renderer)
        {
            BackgroundColor = PipChrome.Background,
            OnRender = () => chrome.Draw(_layout, new ChromeState(compact, showing, onTop, playing, stripTitle)),
            OnResize = (_, _) =>
            {
                _layout = CurrentLayout(window, collapsed);
                if (!collapsed)
                    web.SetBounds(WebViewPixels(_layout, window));
            },
            OnMouseMove = (mx, my) =>
            {
                var hovered = _layout.ButtonAt(mx, my);
                if (hovered == chrome.Hovered)
                    return false;
                chrome.Hovered = hovered;
                return true;
            },
            // Keys reach here only while the strip has focus (after a click on it); in the page, the
            // tweaks script handles the same shortcuts.
            OnKeyDown = key =>
            {
                switch (key.Modifiers, key.Key)
                {
                    case (InputModifier.Ctrl, InputKey.V):
                        OpenPastedLink(web, GetClipboardText());
                        return true;
                    case (InputModifier.Ctrl, InputKey.F):
                        web.Focus();
                        web.PostMessage("""{"search":true}""");
                        return true;
                    // The page knows the video and the time; it answers with a copy message.
                    case (InputModifier.Ctrl, InputKey.C):
                        web.PostMessage("""{"copyLink":"plain"}""");
                        return true;
                    case (InputModifier.Ctrl | InputModifier.Shift, InputKey.C):
                        web.PostMessage("""{"copyLink":"time"}""");
                        return true;
                    default:
                        return false;
                }
            },
            OnMouseDown = (button, mx, my, _, _) =>
            {
                if (button != 1)
                    return false;
                switch (_layout.ButtonAt(mx, my))
                {
                    case PipButton.Back:
                        _ = web.ExecuteScriptAsync("history.back()");
                        break;
                    case PipButton.PlayPause:
                        web.PostMessage("""{"togglePlay":true}""");
                        break;
                    case PipButton.Collapse:
                        SetCollapsed(!collapsed);
                        break;
                    case PipButton.Compact:
                        compact = !compact;
                        web.PostMessage(CompactMessage(compact));
                        break;
                    case PipButton.Pin:
                        onTop = !onTop;
                        window.SetAlwaysOnTop(onTop);
                        break;
                    case PipButton.Close:
                        PushQuit();
                        return true;
                    default:
                        return false;
                }
                // A click on the strip leaves the keyboard on the strip; hand it back to the page, so
                // the next key (space, Ctrl+F) reaches the video. Collapsed, there is no page to give it to.
                if (!collapsed)
                    web.Focus();
                return true;
            },
        };
        // The strip and border light up while the window is the active one, so it is plain when keys go
        // to it. Nothing signals a change, so it is read every iteration (at least every 16 ms).
        var active = false;
        loop.OnLoopIteration = () =>
        {
            var any = false;
            while (UiQueue.TryDequeue(out var action))
            {
                action();
                any = true;
            }
            if (window.IsActive != active)
            {
                active = !active;
                loop.BackgroundColor = active ? PipChrome.ActiveBackground : PipChrome.Background;
                // However the window was activated (a click on the strip or border, the taskbar,
                // Alt+Tab), the keys are for the page, not the strip.
                if (active && !collapsed)
                    web.Focus();
                any = true;
            }
            if (any)
                loop.RequestRedraw();
        };

        loop.Run();

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
        }.Save();

        web.Dispose();
        renderer.Dispose();
        context.Dispose();
        window.Dispose();
        return 0;
    }

    // A left click at (x, y) in the page's CSS pixels that the page takes as the user's own, which a page
    // script's click() is not: YouTube's ad Skip button ignores those. Sent as the DevTools protocol's
    // input events, all three from here, the UI thread, as WebView2 requires; they arrive in order.
    // WebKitGTK has no such protocol, so on Linux an ad's Skip waits for the viewer.
    private static void ClickInPage(INativeWebView web, double x, double y)
    {
        if (!OperatingSystem.IsWindows())
            return;
        var at = $"\"x\":{x.ToString(CultureInfo.InvariantCulture)},\"y\":{y.ToString(CultureInfo.InvariantCulture)}";
        Task.WhenAll(
                web.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", $"{{\"type\":\"mouseMoved\",{at}}}"),
                web.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    $"{{\"type\":\"mousePressed\",{at},\"button\":\"left\",\"clickCount\":1}}"),
                web.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    $"{{\"type\":\"mouseReleased\",{at},\"button\":\"left\",\"clickCount\":1}}"))
            .ContinueWith(t => Console.Error.WriteLine($"[skip] click failed: {t.Exception?.GetBaseException().Message}"),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    // Ctrl+V outside a text field: a YouTube link opens; anything else on the clipboard is left alone.
    private static void OpenPastedLink(INativeWebView web, string? pasted)
    {
        if (YouTubeUrl.FromPastedLink(pasted) is { } url)
            web.Navigate(url);
    }

    private static string CompactMessage(bool compact) => compact ? """{"compact":true}""" : """{"compact":false}""";

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
    private static void Fit(SdlVulkanWindow window, Box current, (int W, int H) size, bool pip)
    {
        // Window coordinates are pixels on Windows and X11, the platforms with a web view backend, so
        // logical sizes scale by the display's scale.
        var min = WindowFit.Scaled(pip ? WindowFit.PipMinimum : WindowFit.BrowseMinimum, window.DisplayScale);
        window.SetMinimumSize(min.W, min.H);
        Place(window, current, (Math.Max(size.W, min.W), Math.Max(size.H, min.H)));
    }

    // Sizes and places the window in place of current, on its display (WindowFit.Place).
    private static void Place(SdlVulkanWindow window, Box current, (int W, int H) size)
    {
        var usable = window.TryGetUsableDisplayBounds(out var ux, out var uy, out var uw, out var uh)
            ? new Box(ux, uy, uw, uh)
            : current with { W = int.MaxValue / 4, H = int.MaxValue / 4 };
        var placed = WindowFit.Place(current, size, usable);
        window.SetSize(placed.W, placed.H);
        window.SetPosition(placed.X, placed.Y);
    }

    // Windows takes the window's icon from the exe's (ApplicationIcon), at every size it needs; X11 has
    // only what the window is given.
    private static void SetIconFromResource(SdlVulkanWindow window)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ytplayer-64.rgba");
        if (stream is null)
            return;
        var icon = new RgbaImage(64, 64);
        stream.ReadExactly(icon.Pixels);
        window.SetIcon(icon);
    }

    private static PipLayout CurrentLayout(SdlVulkanWindow window, bool collapsed)
    {
        GetWindowSize(window.Handle, out var w, out var h);
        return new PipLayout(w, h, collapsed);
    }

    // The layout is in window points; the web view is placed in pixels.
    private static RectInt WebViewPixels(PipLayout layout, SdlVulkanWindow window)
    {
        window.GetSizeInPixels(out var pw, out _);
        var scale = layout.Width > 0 ? pw / (float)layout.Width : 1f;
        var b = layout.WebView;
        var x0 = (int)MathF.Round(b.X * scale);
        var y0 = (int)MathF.Round(b.Y * scale);
        var x1 = (int)MathF.Round((b.X + b.W) * scale);
        var y1 = (int)MathF.Round((b.Y + b.H) * scale);
        return new RectInt(new PointInt(x1, y1), new PointInt(x0, y0));
    }

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
