using static SDL3.SDL;

namespace YtPlayer;

public enum PipButton { None, Back, PlayPause, Next, Collapse, Compact, Pin, Close }

/// <summary>An integer rectangle in window points (SDL's window coordinates).</summary>
internal readonly record struct Box(int X, int Y, int W, int H)
{
    public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// The player window's geometry, in window points: a title strip along the top that drags the window
/// and holds the buttons and the title, the web view below it, and a thin border around the rest that
/// resizes. The window is borderless, so <see cref="HitTest"/> is what gives it the OS's move and resize;
/// the web view covers its own box and takes its own input, so only the strip and the border reach this.
/// <see cref="Collapsed"/>, the window is the strip alone (<see cref="CollapsedHeight"/> tall): no web
/// view, and nothing resizes, as there is nothing to resize to.
/// </summary>
/// <param name="Scale">The display's scale (1 at 96 dpi): window points are pixels on Windows and X11, so
/// the strip's logical sizes are multiplied by it, or at 150% it would be two thirds the size it should.</param>
/// <param name="Fullscreen">The page is fullscreen and the window fills the screen: the web view is all of it,
/// with no strip or border, and nothing drags or resizes.</param>
internal readonly record struct PipLayout(int Width, int Height, bool Collapsed = false, float Scale = 1,
    bool Fullscreen = false)
{
    public int StripHeight => Scaled(28);
    public int Border => Scaled(4);
    public int ButtonWidth => Scaled(34);
    /// <summary>How far from a corner, along either edge, a resize takes the corner.</summary>
    public int CornerGrip => Scaled(14);
    /// <summary>The window's height while collapsed: the strip, and the border under it.</summary>
    public int CollapsedHeight => StripHeight + Border;
    private int TitlePadding => Scaled(6);

    /// <summary>The window's height while collapsed, on a display of <paramref name="scale"/>.</summary>
    public static int CollapsedHeightAt(float scale) => new PipLayout(0, 0, true, scale).CollapsedHeight;

    private int Scaled(int logical) => (int)MathF.Round(logical * Scale);

    public Box WebView => Fullscreen
        ? new(0, 0, Width, Height)
        : Collapsed
            ? default
            : new(Border, StripHeight, Math.Max(0, Width - 2 * Border), Math.Max(0, Height - StripHeight - Border));

    /// <summary>Where the title goes: the strip between the three buttons on the left and the four on the right.</summary>
    public Box Title => new(3 * ButtonWidth + TitlePadding, 0,
        Math.Max(0, Width - 7 * ButtonWidth - 2 * TitlePadding), StripHeight);

    public Box ButtonBox(PipButton button) => button switch
    {
        PipButton.Back => new(0, 0, ButtonWidth, StripHeight),
        PipButton.PlayPause => new(ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Next => new(2 * ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Close => new(Width - ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Pin => new(Width - 2 * ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Compact => new(Width - 3 * ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Collapse => new(Width - 4 * ButtonWidth, 0, ButtonWidth, StripHeight),
        _ => default,
    };

    public static ReadOnlySpan<PipButton> Buttons =>
        [PipButton.Back, PipButton.PlayPause, PipButton.Next, PipButton.Collapse, PipButton.Compact, PipButton.Pin, PipButton.Close];

    public PipButton ButtonAt(float x, float y)
    {
        // The top Border rows resize, so a button starts below them (collapsed, nothing resizes).
        if (Fullscreen || (y < Border && !Collapsed))
            return PipButton.None;
        foreach (var button in Buttons)
            if (ButtonBox(button).Contains(x, y))
                return button;
        return PipButton.None;
    }

    public HitTestResult HitTest(float x, float y)
    {
        if (Fullscreen)
            return HitTestResult.Normal;
        if (Collapsed)
            return ButtonAt(x, y) != PipButton.None ? HitTestResult.Normal : HitTestResult.Draggable;

        var left = x < Border;
        var right = x >= Width - Border;
        var top = y < Border;
        var bottom = y >= Height - Border;
        var nearLeft = x < CornerGrip;
        var nearRight = x >= Width - CornerGrip;
        var nearTop = y < CornerGrip;
        var nearBottom = y >= Height - CornerGrip;

        if ((top && nearLeft) || (left && nearTop)) return HitTestResult.ResizeTopLeft;
        if ((top && nearRight) || (right && nearTop)) return HitTestResult.ResizeTopRight;
        if ((bottom && nearLeft) || (left && nearBottom)) return HitTestResult.ResizeBottomLeft;
        if ((bottom && nearRight) || (right && nearBottom)) return HitTestResult.ResizeBottomRight;
        if (top) return HitTestResult.ResizeTop;
        if (bottom) return HitTestResult.ResizeBottom;
        if (left) return HitTestResult.ResizeLeft;
        if (right) return HitTestResult.ResizeRight;

        if (ButtonAt(x, y) != PipButton.None)
            return HitTestResult.Normal;
        return y < StripHeight ? HitTestResult.Draggable : HitTestResult.Normal;
    }
}
