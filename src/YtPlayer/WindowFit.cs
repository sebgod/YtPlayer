namespace YtPlayer;

/// <summary>
/// The window has two sizes: small while a compact video shows (the picture in picture), and bigger
/// for browsing, where YouTube's page needs room for its masthead, search and titles. This is what
/// size a switch between them gives the window; the window resizes in place to it
/// (SdlVulkanWindow.ResizeInPlace). The sizes here are design units (1 = 96 dpi), which the window turns
/// into its coordinates (SdlVulkanWindow.ToWindowSize); Switch works in window coordinates.
/// </summary>
internal static class WindowFit
{
    // As wide as the strip's twelve buttons, which a narrower window would cut off on the right.
    public static readonly (int W, int H) PipMinimum = (348, 170);
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
}
