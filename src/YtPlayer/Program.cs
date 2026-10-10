using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        var window = app.CreateWindow("YtPlayer", settings.Width, settings.Height,
            maximized: false, borderless: true, alwaysOnTop: onTop);
        if (settings is { X: { } x, Y: { } y })
            SetWindowPosition(window.Handle, x, y);
        SetWindowMinimumSize(window.Handle, 240, 170);

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
        web.MessageReceived += json => UiQueue.Enqueue(() => OnPageMessage(json, web, compact));
        web.TitleChanged += title => UiQueue.Enqueue(() => window.SetTitle(TitleFor(title)));
        web.PageError += error => Console.Error.WriteLine($"[page] {error}");
        web.AttachToWindow(window);
        web.SetBounds(WebViewPixels(_layout, window));
        web.Navigate(startUrl);

        var loop = new SdlEventLoop(window, renderer)
        {
            BackgroundColor = PipChrome.Background,
            OnRender = () => chrome.Draw(_layout, compact, onTop),
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
        loop.OnLoopIteration = () =>
        {
            var any = false;
            while (UiQueue.TryDequeue(out var action))
            {
                action();
                any = true;
            }
            if (any)
                loop.RequestRedraw();
        };

        loop.Run();

        window.GetPosition(out var px, out var py);
        GetWindowSize(window.Handle, out var w, out var h);
        new PlayerSettings { X = px, Y = py, Width = w, Height = h, OnTop = onTop, Compact = compact }.Save();

        web.Dispose();
        renderer.Dispose();
        context.Dispose();
        window.Dispose();
        return 0;
    }

    // The page asks for the compact state when it is ready (every full load), since a document-start
    // script cannot know it.
    private static void OnPageMessage(string json, INativeWebView web, bool compact)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("ready", out _))
                web.PostMessage(CompactMessage(compact));
        }
        catch (JsonException)
        {
            // Not ours.
        }
    }

    private static string CompactMessage(bool compact) => compact ? """{"compact":true}""" : """{"compact":false}""";

    private static string TitleFor(string pageTitle)
    {
        const string suffix = " - YouTube";
        var title = pageTitle.EndsWith(suffix, StringComparison.Ordinal) ? pageTitle[..^suffix.Length] : pageTitle;
        return string.IsNullOrWhiteSpace(title) ? "YtPlayer" : title;
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
