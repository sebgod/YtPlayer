using DIR.Lib;
using SdlVulkan.Renderer;

namespace YtPlayer;

/// <summary>What the strip shows besides its buttons' shapes.</summary>
/// <param name="Compact">Compact is on (the user's choice).</param>
/// <param name="Showing">Compact is on screen (a watch page, not searching).</param>
/// <param name="OnTop">The window stays on top.</param>
/// <param name="Playing">The player's video is playing.</param>
/// <param name="Title">The page's title, for the strip; null shows none.</param>
internal readonly record struct ChromeState(bool Compact, bool Showing, bool OnTop, bool Playing, string? Title);

/// <summary>Draws the strip and border around the web view: the buttons, which one the pointer is over,
/// and the title. Everything the window shows besides the page itself.</summary>
/// <param name="font">A font file for the title (DIR.Lib's FontResolver); null leaves the title out.</param>
internal sealed class PipChrome(VkRenderer renderer, string? font)
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
    private const float TitleSize = 12;

    // The last title fitted to the strip, so a redraw does not measure it again.
    private (string Title, float Width, string Fitted) _fittedTitle = ("", 0, "");

    public PipButton Hovered { get; set; }

    public void Draw(PipLayout layout, ChromeState state)
    {
        if (layout.Width <= 0)
            return;
        // Window points to the renderer's pixels.
        var scale = renderer.Width / (float)layout.Width;
        // Logical sizes (a mark, a line, the title's text) to the renderer's pixels: the display's scale too.
        var ui = scale * layout.Scale;

        foreach (var button in PipLayout.Buttons)
        {
            var b = layout.ButtonBox(button);
            var x0 = b.X * scale;
            var y0 = b.Y * scale;
            var w = b.W * scale;
            var h = b.H * scale;
            if (button == Hovered)
                renderer.FillRectangle(Rect(x0, y0, w, h), button == PipButton.Close ? CloseHover : Hover);

            var cx = x0 + w / 2;
            var cy = y0 + h / 2;
            var u = 5 * ui; // half the size of a mark
            var t = Math.Max(1, (int)MathF.Round(1.5f * ui));
            switch (button)
            {
                case PipButton.Back:
                    renderer.DrawLine(cx + u / 2, cy - u, cx - u / 2, cy, Ink, t);
                    renderer.DrawLine(cx - u / 2, cy, cx + u / 2, cy + u, Ink, t);
                    break;
                case PipButton.PlayPause:
                    // What a press does: two bars while playing, a triangle while paused.
                    if (state.Playing)
                    {
                        renderer.FillRectangle(Rect(cx - u * 0.8f, cy - u, u * 0.55f, u * 2), Ink);
                        renderer.FillRectangle(Rect(cx + u * 0.25f, cy - u, u * 0.55f, u * 2), Ink);
                    }
                    else
                        FillPlayTriangle(cx - u * 0.6f, cy, u * 1.6f, u);
                    break;
                case PipButton.Collapse:
                    // A chevron: up folds the window to the strip, down unfolds it.
                    var dy = layout.Collapsed ? u / 2 : -u / 2;
                    renderer.DrawLine(cx - u, cy - dy, cx, cy + dy, Ink, t);
                    renderer.DrawLine(cx, cy + dy, cx + u, cy - dy, Ink, t);
                    break;
                case PipButton.Compact:
                    // A screen; in compact mode with the small picture filled, showing what is on.
                    // Compact on away from a video only outlines it: it waits for the next video.
                    renderer.DrawRectangle(Rect(cx - u * 1.3f, cy - u, u * 2.6f, u * 2), Ink, t);
                    var picture = Rect(cx, cy, u * 1.1f, u * 0.8f);
                    if (state.Compact && state.Showing)
                        renderer.FillRectangle(picture, Accent);
                    else if (state.Compact)
                        renderer.DrawRectangle(picture, Accent, t);
                    break;
                case PipButton.Pin:
                    // A dot: filled while the window stays on top.
                    var dot = Rect(cx - u * 0.8f, cy - u * 0.8f, u * 1.6f, u * 1.6f);
                    if (state.OnTop)
                        renderer.FillEllipse(dot, Accent);
                    else
                        renderer.DrawEllipseOutline(dot, Ink, t);
                    break;
                case PipButton.Close:
                    renderer.DrawLine(cx - u, cy - u, cx + u, cy + u, Ink, t);
                    renderer.DrawLine(cx - u, cy + u, cx + u, cy - u, Ink, t);
                    break;
            }
        }

        DrawTitle(layout.Title, state.Title, scale, ui);
    }

    private void DrawTitle(Box box, string? title, float scale, float ui)
    {
        var width = box.W * scale;
        if (font is null || string.IsNullOrWhiteSpace(title) || width < 24 * ui)
            return;
        var size = TitleSize * ui;
        if (_fittedTitle.Title != title || _fittedTitle.Width != width)
            _fittedTitle = (title, width, Ellipsize(title, size, width));
        if (_fittedTitle.Fitted.Length == 0)
            return;
        renderer.DrawText(_fittedTitle.Fitted, font, size, TitleInk,
            Rect(box.X * scale, box.Y * scale, width, box.H * scale), TextAlign.Near, TextAlign.Center);
    }

    // The longest start of text that fits in maxWidth with an ellipsis after it, or all of it if it fits.
    private string Ellipsize(string text, float size, float maxWidth)
    {
        if (renderer.MeasureText(text, font!, size).Width <= maxWidth)
            return text;
        int fits = 0, tooLong = text.Length;
        while (tooLong - fits > 1)
        {
            var mid = (fits + tooLong) / 2;
            if (renderer.MeasureText(string.Concat(text.AsSpan(0, mid).TrimEnd(), "…"), font!, size).Width <= maxWidth)
                fits = mid;
            else
                tooLong = mid;
        }
        return fits == 0 ? "" : string.Concat(text.AsSpan(0, fits).TrimEnd(), "…");
    }

    // A right-pointing triangle: its left edge at x, width wide, 2 * halfHeight tall about cy. Filled a row
    // at a time, as the renderer has no polygon fill.
    private void FillPlayTriangle(float x, float cy, float width, float halfHeight)
    {
        var top = (int)MathF.Floor(cy - halfHeight);
        var bottom = (int)MathF.Ceiling(cy + halfHeight);
        for (var row = top; row < bottom; row++)
        {
            var reach = 1 - MathF.Abs(row + 0.5f - cy) / halfHeight;
            if (reach > 0)
                renderer.FillRectangle(Rect(x, row, width * reach, 1), Ink);
        }
    }

    // RectInt takes (LowerRight, UpperLeft).
    private static RectInt Rect(float x, float y, float w, float h)
        => new(new PointInt((int)MathF.Round(x + w), (int)MathF.Round(y + h)), new PointInt((int)MathF.Round(x), (int)MathF.Round(y)));
}
