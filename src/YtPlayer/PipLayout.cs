using static SDL3.SDL;

namespace YtPlayer;

public enum PipButton { None, Back, Compact, Pin, Close }

/// <summary>An integer rectangle in window points (SDL's window coordinates).</summary>
internal readonly record struct Box(int X, int Y, int W, int H)
{
    public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// The player window's geometry, in window points: a title strip along the top that drags the window
/// and holds the buttons, the web view below it, and a thin border around the rest that resizes. The
/// window is borderless, so <see cref="HitTest"/> is what gives it the OS's move and resize; the web view
/// covers its own box and takes its own input, so only the strip and the border reach this.
/// </summary>
internal readonly record struct PipLayout(int Width, int Height)
{
    public const int StripHeight = 28;
    public const int Border = 4;
    public const int ButtonWidth = 34;
    /// <summary>How far from a corner, along either edge, a resize takes the corner.</summary>
    public const int CornerGrip = 14;

    public Box WebView => new(Border, StripHeight, Math.Max(0, Width - 2 * Border), Math.Max(0, Height - StripHeight - Border));

    public Box ButtonBox(PipButton button) => button switch
    {
        PipButton.Back => new(0, 0, ButtonWidth, StripHeight),
        PipButton.Close => new(Width - ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Pin => new(Width - 2 * ButtonWidth, 0, ButtonWidth, StripHeight),
        PipButton.Compact => new(Width - 3 * ButtonWidth, 0, ButtonWidth, StripHeight),
        _ => default,
    };

    public static ReadOnlySpan<PipButton> Buttons => [PipButton.Back, PipButton.Compact, PipButton.Pin, PipButton.Close];

    public PipButton ButtonAt(float x, float y)
    {
        // The top Border rows resize, so a button starts below them.
        if (y < Border)
            return PipButton.None;
        foreach (var button in Buttons)
            if (ButtonBox(button).Contains(x, y))
                return button;
        return PipButton.None;
    }

    public HitTestResult HitTest(float x, float y)
    {
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
