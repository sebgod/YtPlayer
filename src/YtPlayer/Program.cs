using System.Collections.Concurrent;
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
        if (OperatingSystem.IsWindows())
            Win32Window.RemoveMaximize(window.Handle);
        else
            SetIconFromResource(window);

        // Compact came on screen or left it: keep the size the window is leaving, take the other.
        void ShowCompact(bool on)
        {
            if (on == showing)
                return;
            GetWindowSize(window.Handle, out var w, out var h);
            if (showing)
                pipSize = (w, h);
            else
                browseSize = (w, h);
            showing = on;
            window.GetPosition(out var wx, out var wy);
            Fit(window, new Box(wx, wy, w, h), on ? pipSize : browseSize, on);
        }

        window.GetSizeInPixels(out var pixelW, out var pixelH);
        var context = VulkanContext.CreateForSharedDevice(app.Device, window.Surface, (uint)pixelW, (uint)pixelH);
        var renderer = new VkRenderer(context, (uint)pixelW, (uint)pixelH);
        var chrome = new PipChrome(renderer);
        _layout = CurrentLayout(window);

        _hitTest = (nint _, in Point area, nint _) => _layout.HitTest(area.X, area.Y);
        SetWindowHitTest(window.Handle, _hitTest, nint.Zero);

        var web = NativeWebView.Create(new NativeWebViewOptions { UserDataFolder = PlayerSettings.WebProfileFolder });
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
        });
        web.TitleChanged += title => UiQueue.Enqueue(() => window.SetTitle(TitleFor(title)));
        web.PageError += error => Console.Error.WriteLine($"[page] {error}");
        web.AttachToWindow(window);
        web.SetBounds(WebViewPixels(_layout, window));
        web.Navigate(startUrl);

        var loop = new SdlEventLoop(window, renderer)
        {
            BackgroundColor = PipChrome.Background,
            OnRender = () => chrome.Draw(_layout, compact, showing, onTop),
            OnResize = (_, _) =>
            {
                _layout = CurrentLayout(window);
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
            // tweaks script handles the same two shortcuts.
            OnKeyDown = key =>
            {
                if (key.Modifiers != InputModifier.Ctrl)
                    return false;
                switch (key.Key)
                {
                    case InputKey.V:
                        OpenPastedLink(web, GetClipboardText());
                        return true;
                    case InputKey.F:
                        web.Focus();
                        web.PostMessage("""{"search":true}""");
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
                        return true;
                    case PipButton.Compact:
                        compact = !compact;
                        web.PostMessage(CompactMessage(compact));
                        return true;
                    case PipButton.Pin:
                        onTop = !onTop;
                        window.SetAlwaysOnTop(onTop);
                        return true;
                    case PipButton.Close:
                        PushQuit();
                        return true;
                    default:
                        return false;
                }
            },
        };
        // The strip and border light up while the window is the active one. Checked every iteration (the
        // loop wakes at least every 16 ms), as no SDL event covers it: see Win32Window.IsForeground.
        var hwnd = OperatingSystem.IsWindows() ? Win32Window.Handle(window.Handle) : nint.Zero;
        var active = false;
        loop.OnLoopIteration = () =>
        {
            var any = false;
            while (UiQueue.TryDequeue(out var action))
            {
                action();
                any = true;
            }
            var nowActive = OperatingSystem.IsWindows()
                ? Win32Window.IsForeground(hwnd)
                : (GetWindowFlags(window.Handle) & WindowFlags.InputFocus) != 0;
            if (nowActive != active)
            {
                active = nowActive;
                loop.BackgroundColor = active ? PipChrome.ActiveBackground : PipChrome.Background;
                any = true;
            }
            if (any)
                loop.RequestRedraw();
        };

        loop.Run();

        window.GetPosition(out var px, out var py);
        GetWindowSize(window.Handle, out var exitW, out var exitH);
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

    // Sets the mode's minimum, then sizes and places the window in place of current, on its display.
    private static void Fit(SdlVulkanWindow window, Box current, (int W, int H) size, bool pip)
    {
        // Window coordinates are pixels on Windows and X11, the platforms with a web view backend, so
        // logical sizes scale by the display's scale.
        var min = WindowFit.Scaled(pip ? WindowFit.PipMinimum : WindowFit.BrowseMinimum, window.DisplayScale);
        SetWindowMinimumSize(window.Handle, min.W, min.H);
        var target = (Math.Max(size.W, min.W), Math.Max(size.H, min.H));
        var usable = GetDisplayUsableBounds(GetDisplayForWindow(window.Handle), out var r)
            ? new Box(r.X, r.Y, r.W, r.H)
            : current with { W = int.MaxValue / 4, H = int.MaxValue / 4 };
        var placed = WindowFit.Place(current, target, usable);
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

    private static PipLayout CurrentLayout(SdlVulkanWindow window)
    {
        GetWindowSize(window.Handle, out var w, out var h);
        return new PipLayout(w, h);
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
