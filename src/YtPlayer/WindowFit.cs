namespace YtPlayer;

/// <summary>
/// The window has two sizes: small while a compact video shows (the picture in picture), and bigger
/// for browsing, where YouTube's page needs room for its masthead, search and titles. This is where
/// a switch between them puts the window. Sizes in logical units (1 = 96 dpi) are scaled by the
/// window's content scale; everything else is in window coordinates.
/// </summary>
internal static class WindowFit
{
    public static readonly (int W, int H) PipMinimum = (240, 170);
    // About 16:9 for the video once the strip and border are taken off; 360x230 was too small to watch.
    public static readonly (int W, int H) PipDefault = (480, 300);
    public static readonly (int W, int H) BrowseMinimum = (560, 480);
    public static readonly (int W, int H) BrowseDefault = (600, 520);

    /// <summary>The size a switch goes to: the new mode's remembered height, and the width the window
    /// has now, so it keeps its place instead of jumping sideways. Browsing may need it wider than a
    /// narrow PiP; then the PiP's own width is kept, to give back on the way back.</summary>
    /// <returns>The size, and the narrow PiP width to give back later (null when there is none).</returns>
    public static ((int W, int H) Size, int? NarrowPipWidth) Switch(
        bool toPip, int currentWidth, int height, int browseMinimumWidth, int? narrowPipWidth)
        => toPip
            ? ((narrowPipWidth ?? currentWidth, height), null)
            : currentWidth < browseMinimumWidth
                ? ((browseMinimumWidth, height), currentWidth)
                : ((currentWidth, height), null);

    public static (int W, int H) Scaled((int W, int H) logical, float scale)
        => ((int)MathF.Round(logical.W * scale), (int)MathF.Round(logical.H * scale));

    /// <summary>Where a window of <paramref name="size"/> goes in place of <paramref name="current"/>. The
    /// corner nearest the display's own corner stays put, so a PiP parked bottom right grows up and to the
    /// left; the result is kept inside <paramref name="usable"/> (the display less its taskbar).</summary>
    public static Box Place(Box current, (int W, int H) size, Box usable)
    {
        var w = Math.Min(size.W, usable.W);
        var h = Math.Min(size.H, usable.H);
        var anchorRight = current.X + current.W / 2 > usable.X + usable.W / 2;
        var anchorBottom = current.Y + current.H / 2 > usable.Y + usable.H / 2;
        var x = anchorRight ? current.X + current.W - w : current.X;
        var y = anchorBottom ? current.Y + current.H - h : current.Y;
        x = Math.Clamp(x, usable.X, usable.X + usable.W - w);
        y = Math.Clamp(y, usable.Y, usable.Y + usable.H - h);
        return new Box(x, y, w, h);
    }
}
