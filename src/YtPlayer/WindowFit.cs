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
    public static readonly (int W, int H) PipDefault = (360, 230);
    public static readonly (int W, int H) BrowseMinimum = (560, 480);
    public static readonly (int W, int H) BrowseDefault = (600, 520);

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
