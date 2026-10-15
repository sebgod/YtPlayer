using DIR.Lib;
using static SDL3.SDL;
using B = DIR.Lib.Layout.Builder;
using Layout = DIR.Lib.Layout;

namespace YtPlayer;

public enum PipButton { Back, Home, Previous, PlayPause, Next, Collapse, Compact, Pin, Hide, Fullscreen, Close }

/// <summary>How the window shows the page.</summary>
internal enum PipMode
{
    /// <summary>The strip along the top, the page below it, and a thin border around the rest that resizes.</summary>
    Normal,

    /// <summary>The strip alone (the collapse button), the video playing on behind it: no page, and nothing
    /// resizes, as there is nothing to resize to.</summary>
    Collapsed,

    /// <summary>The page is fullscreen and the window fills the screen: the page is all of it, with no strip or
    /// border, and nothing drags or resizes.</summary>
    Fullscreen,
}

/// <summary>What the window shows besides the page itself.</summary>
/// <param name="Mode">Strip and page, the strip alone, or the page alone.</param>
/// <param name="Active">The window is the active one (<see cref="PipChrome{TSurface}.ActiveBackground"/>).</param>
/// <param name="Compact">Compact is on (the user's choice).</param>
/// <param name="Showing">Compact is on screen (a watch page, not searching).</param>
/// <param name="OnTop">The window stays on top.</param>
/// <param name="Playing">The player's video is playing.</param>
/// <param name="Title">The page's title, for the strip; null shows none.</param>
internal readonly record struct ChromeState(PipMode Mode, bool Active, bool Compact, bool Showing, bool OnTop,
    bool Playing, string? Title);

/// <summary>What a press on the window's frame does, for SDL's hit test: the strip drags the window, the border
/// resizes it from an edge or a corner.</summary>
internal sealed record FrameHit(HitTestResult Area) : HitResult;

/// <summary>
/// Everything the window shows besides the page: the strip along the top (back and home, then previous,
/// play/pause and next, the title, collapse, compact, stay on top, hide, fullscreen, close) and the border around
/// the page, as one <see cref="Layout.Node"/> tree.
/// Its sizes are design units, which the measure context maps to pixels at <see cref="PixelWidgetBase{TSurface}.DpiScale"/>
/// (the display's, which the host sets). The painter draws the tree and binds each button's press to the rect it
/// drew it in; the router reads those regions for the clicks and the hover, SDL's hit test asks them what drags
/// and what resizes, and the web view goes where the tree's web leaf was arranged. Nothing is placed by hand, so
/// what is drawn, what is pressed and where the page sits cannot disagree.
/// </summary>
/// <remarks>The window is borderless, so the hit test (<see cref="FrameAreaAt"/>) is what gives it the OS's move
/// and resize. The web view covers its own rect and takes its own input, so only the strip and the border reach
/// this.</remarks>
internal sealed class PipChrome<TSurface>(Renderer<TSurface> renderer) : PixelWidgetBase<TSurface>(renderer)
{
    public static readonly RGBAColor32 Background = new(0x18, 0x18, 0x1c, 0xff);
    /// <summary>The strip and border while the window is the active one, so it is plain when keys go to it
    /// (an always-on-top window can take them without the user noticing).</summary>
    public static readonly RGBAColor32 ActiveBackground = new(0x3a, 0x3a, 0x4a, 0xff);
    // Lighter than both backgrounds, so a hovered button still stands out on an active strip.
    private static readonly RGBAColor32 Hover = new(0x52, 0x52, 0x64, 0xff);
    private static readonly RGBAColor32 CloseHover = new(0xc4, 0x2b, 0x1c, 0xff);
    private static readonly RGBAColor32 Ink = new(0xd8, 0xd8, 0xdc, 0xff);
    private static readonly RGBAColor32 TitleInk = new(0xb4, 0xb4, 0xbc, 0xff);
    private static readonly RGBAColor32 Accent = new(0xff, 0x30, 0x30, 0xff);

    // Design units, mapped to pixels by the measure context.
    private const float StripHeight = 28;
    private const float Border = 4;
    // Eleven of them and the gap between the groups fit the narrowest window (WindowFit.PipMinimum).
    private const float ButtonWidth = 28;
    // Between the page's buttons (back, home) and the video's (previous, play/pause, next).
    private const float GroupGap = 6;
    // How far from a corner, along either edge, a resize takes the corner.
    private const float CornerGrip = 14;
    private const float MarkSize = 10;
    private const float MarkStroke = 1.5f;
    private const float TitleSize = 12;
    private const float TitlePadding = 6;
    // A title with less room than this is left out rather than cut to an ellipsis alone.
    private const float TitleMinimum = 24;

    // The Fill leaves: the page's web view, and the marks no IconKind names, which DrawFill draws.
    private const string WebViewKey = "web";
    private const string BackMark = "back";
    private const string HomeMark = "home";
    private const string CompactMark = "compact";
    private const string PinMark = "pin";
    private const string HideMark = "hide";
    private const string FullscreenMark = "fullscreen";
    private const string CloseMark = "close";

    // What the last Render showed, for the marks DrawFill draws.
    private ChromeState _painted;

    /// <summary>What a press on a strip button does; the host's.</summary>
    public Action<PipButton>? Pressed { get; set; }

    /// <summary>Whether the strip has a fullscreen button: where the host can put the page fullscreen from it and
    /// the window follows (WebView2's). Without it, hide sits beside close.</summary>
    public bool CanFullscreen { get; init; } = true;

    /// <summary>Paints the tree over the whole surface, binding the presses to where it drew.</summary>
    public void Render(ChromeState state)
    {
        _painted = state;
        BeginFrame();
        RenderLayout(Build(state), new RectF32(0, 0, Renderer.Width, Renderer.Height), drawFill: DrawFill);
    }

    /// <summary>Where the web view goes in a surface of this size, in pixels: the tree's web leaf, arranged as
    /// the next paint will arrange it. Empty while collapsed, when there is no page to show.</summary>
    public RectInt WebViewBounds(ChromeState state, int width, int height)
    {
        foreach (var (node, rect) in ArrangeLayout(Build(state), new RectF32(0, 0, width, height)))
        {
            if (node is Layout.Node.Leaf { Content: Layout.Content.Fill { Key: WebViewKey } })
                return Pixels(rect.X, rect.Y, rect.Width, rect.Height);
        }
        return default;
    }

    /// <summary>How tall the window is collapsed, in design units: the strip and the border under it, measured.
    /// The window turns it into its own coordinates (SdlVulkanWindow.ToWindowSize).</summary>
    public float CollapsedHeight()
        => MeasureLayout(Build(default(ChromeState) with { Mode = PipMode.Collapsed }),
            new Layout.Size<float>(Scale.ToDesignX(Renderer.Width), float.MaxValue), scale: DesignScale.One).Height;

    /// <summary>What SDL's hit test answers at (x, y) in pixels: what the last paint put there. The strip drags,
    /// the border resizes, and a button or the page is the window's own.</summary>
    public HitTestResult FrameAreaAt(float x, float y)
        => HitTest(x, y) is FrameHit frame ? frame.Area : HitTestResult.Normal;

    internal Layout.Node Build(ChromeState state)
    {
        var page = B.Fill(key: WebViewKey).Stretch();
        var background = state.Active ? ActiveBackground : Background;
        return state.Mode switch
        {
            // Fullscreen, the page is all there is.
            PipMode.Fullscreen => page,
            // Collapsed, all of it drags but the buttons.
            PipMode.Collapsed => B.VStack(Strip(state), B.Spacer().RowH(Border))
                .Bg(background)
                .Clickable(new FrameHit(HitTestResult.Draggable)),
            _ => B.Overlay(
                B.Dock(page,
                        B.Top(Strip(state), StripHeight),
                        B.Left(B.Spacer(), Border),
                        B.Right(B.Spacer(), Border),
                        B.Bottom(B.Spacer(), Border))
                    .Bg(background),
                Grips()),
        };
    }

    // The buttons and the title. All of it drags but the buttons.
    private Layout.Node Strip(ChromeState state)
        => B.HStack([
                // The page's, in strokes. Back is a browser's arrow, not a triangle, which beside play would read as
                // play's reverse.
                Button(PipButton.Back, B.Fill(MarkSize, MarkSize, BackMark)),
                Button(PipButton.Home, B.Fill(MarkSize, MarkSize, HomeMark)),
                B.Spacer().ColW(GroupGap),
                // The video's: filled. The previous video, next's mirror: a bar against a triangle.
                Button(PipButton.Previous,
                    B.HStack(B.Box(2.25f, 8, Ink), B.Icon(Layout.IconKind.CaretLeft, 8, Ink)).WithGap(0.5f)),
                // What a press does: two bars while playing, a triangle while paused.
                Button(PipButton.PlayPause, state.Playing
                    ? B.HStack(B.Box(2.75f, MarkSize, Ink), B.Box(2.75f, MarkSize, Ink)).WithGap(2.5f)
                    : B.Icon(Layout.IconKind.CaretRight, MarkSize, Ink)),
                // The next video: a triangle against a bar.
                Button(PipButton.Next,
                    B.HStack(B.Icon(Layout.IconKind.CaretRight, 8, Ink), B.Box(2.25f, 8, Ink)).WithGap(0.5f)),
                Title(state.Title),
                // Up folds the window to the strip, down unfolds it.
                Button(PipButton.Collapse, B.Icon(
                    state.Mode == PipMode.Collapsed ? Layout.IconKind.CaretDown : Layout.IconKind.CaretUp, 8, Ink)),
                Button(PipButton.Compact, B.Fill(13, MarkSize, CompactMark)),
                Button(PipButton.Pin, B.Fill(8, 8, PinMark)),
                Button(PipButton.Hide, B.Fill(MarkSize, MarkSize, HideMark)),
                .. CanFullscreen
                    ? new[] { Button(PipButton.Fullscreen, B.Fill(MarkSize, MarkSize, FullscreenMark)) }
                    : [],
                Button(PipButton.Close, B.Fill(MarkSize, MarkSize, CloseMark))])
            .RowH(StripHeight)
            .Clickable(new FrameHit(HitTestResult.Draggable));

    // A button: its mark centred, lit while the pointer is over it.
    private Layout.Node Button(PipButton button, Layout.Node mark)
        => B.HStack(B.Spacer().WStar(), mark, B.Spacer().WStar())
            .Align(Layout.CrossAlign.Center)
            .ColW(ButtonWidth)
            .BgHover(button == PipButton.Close ? CloseHover : Hover)
            .Clickable(new HitResult.ButtonHit(button.ToString()), _ => Pressed?.Invoke(button));

    // The page's title, cut to an ellipsis to fit, between the five buttons on the left and the six on the right
    // (whose room it keeps when there is none).
    private Layout.Node Title(string? title)
        => B.HStack(string.IsNullOrWhiteSpace(title) || FontPath.Length == 0
                ? B.Spacer()
                : B.Text(title, TitleSize, TitleInk).Stretch().CollapseBelow(TitleMinimum))
            .Pad(TitlePadding, 0)
            .Stretch();

    // The border resizes: along each edge, and from the corner within CornerGrip of one. Over the strip's top rows
    // too, so the top edge resizes above the buttons.
    private static Layout.Node Grips()
    {
        static Layout.Node Grip(HitTestResult area) => B.Spacer().Stretch().Clickable(new FrameHit(area));
        // The left and right edges run between the top and bottom ones, which already have a corner's first Border.
        const float side = CornerGrip - Border;
        return B.Dock(B.Spacer(),
            B.Top(B.HStack(Grip(HitTestResult.ResizeTopLeft).ColW(CornerGrip), Grip(HitTestResult.ResizeTop),
                Grip(HitTestResult.ResizeTopRight).ColW(CornerGrip)), Border),
            B.Bottom(B.HStack(Grip(HitTestResult.ResizeBottomLeft).ColW(CornerGrip), Grip(HitTestResult.ResizeBottom),
                Grip(HitTestResult.ResizeBottomRight).ColW(CornerGrip)), Border),
            B.Left(B.VStack(Grip(HitTestResult.ResizeTopLeft).RowH(side), Grip(HitTestResult.ResizeLeft),
                Grip(HitTestResult.ResizeBottomLeft).RowH(side)), Border),
            B.Right(B.VStack(Grip(HitTestResult.ResizeTopRight).RowH(side), Grip(HitTestResult.ResizeRight),
                Grip(HitTestResult.ResizeBottomRight).RowH(side)), Border));
    }

    // The marks no IconKind names, each drawn in the rect its leaf was arranged into; the web view's leaf is the
    // native view's to cover.
    private void DrawFill(Layout.Content.Fill fill, RectF32 r)
    {
        var stroke = Math.Max(1, (int)MathF.Round(Scale.ToSurface(MarkStroke)));
        switch (fill.Key)
        {
            case BackMark:
            {
                // An arrow pointing back: a shaft, and the head's two strokes from its tip.
                var middle = r.Y + r.Height / 2;
                var head = r.Height / 2;
                DrawLine(r.X, middle, r.Right, middle, Ink, stroke);
                DrawLine(r.X, middle, r.X + head, r.Y, Ink, stroke);
                DrawLine(r.X, middle, r.X + head, r.Bottom, Ink, stroke);
                break;
            }
            case HomeMark:
            {
                // A house: a roof over two walls and the floor between them. Five strokes in the space the others
                // draw two in, so thinner, or at 100% they run together.
                var line = Math.Max(1, (int)MathF.Round(Scale.ToSurface(MarkStroke * 0.8f)));
                var eaves = r.Y + r.Height * 0.5f;
                var left = r.X + r.Width * 0.16f;
                var right = r.Right - r.Width * 0.16f;
                var walls = eaves - r.Height * 0.12f;
                DrawLine(r.X, eaves, r.X + r.Width / 2, r.Y, Ink, line);
                DrawLine(r.X + r.Width / 2, r.Y, r.Right, eaves, Ink, line);
                Renderer.FillRectangle(Pixels(left, walls, line, r.Bottom - walls), Ink);
                Renderer.FillRectangle(Pixels(right - line, walls, line, r.Bottom - walls), Ink);
                Renderer.FillRectangle(Pixels(left, r.Bottom - line, right - left, line), Ink);
                break;
            }
            case CompactMark:
                // A screen; in compact mode with the small picture filled, showing what is on.
                // Compact on away from a video only outlines it: it waits for the next video.
                Renderer.DrawRectangle(Pixels(r.X, r.Y, r.Width, r.Height), Ink, stroke);
                var picture = Pixels(r.X + r.Width / 2, r.Y + r.Height / 2, r.Width * 0.42f, r.Height * 0.4f);
                if (_painted is { Compact: true, Showing: true })
                    Renderer.FillRectangle(picture, Accent);
                else if (_painted.Compact)
                    Renderer.DrawRectangle(picture, Accent, stroke);
                break;
            case PinMark:
                // A dot: filled while the window stays on top.
                if (_painted.OnTop)
                    FillEllipse(r.X, r.Y, r.Width, r.Height, Accent);
                else
                    DrawEllipse(r.X, r.Y, r.Width, r.Height, Ink, stroke);
                break;
            case FullscreenMark:
            {
                // YouTube's own: a screen's four corners. A square, Windows' maximize between minimize and close,
                // promised what does not happen (the page goes fullscreen, the window does not maximize) and looked
                // like compact's screen beside it.
                var arm = r.Width * 0.38f;
                void Corner(float x, float y, bool right, bool bottom)
                {
                    Renderer.FillRectangle(Pixels(right ? x - arm : x, bottom ? y - stroke : y, arm, stroke), Ink);
                    Renderer.FillRectangle(Pixels(right ? x - stroke : x, bottom ? y - arm : y, stroke, arm), Ink);
                }
                Corner(r.X, r.Y, right: false, bottom: false);
                Corner(r.Right, r.Y, right: true, bottom: false);
                Corner(r.X, r.Bottom, right: false, bottom: true);
                Corner(r.Right, r.Bottom, right: true, bottom: true);
                break;
            }
            case HideMark:
            {
                // A bar, as Windows draws minimize beside close: out of the way. A crossed-out eye ran together at
                // 100%, and an arrow down onto a line read as a download.
                var middle = r.Y + r.Height / 2;
                DrawLine(r.X, middle, r.Right, middle, Ink, stroke);
                break;
            }
            case CloseMark:
                DrawLine(r.X, r.Y, r.Right, r.Bottom, Ink, stroke);
                DrawLine(r.X, r.Bottom, r.Right, r.Y, Ink, stroke);
                break;
        }
    }

    // RectInt takes (LowerRight, UpperLeft).
    private static RectInt Pixels(float x, float y, float w, float h)
        => new(new PointInt((int)MathF.Round(x + w), (int)MathF.Round(y + h)), new PointInt((int)MathF.Round(x), (int)MathF.Round(y)));
}
