using DIR.Lib;
using SdlVulkan.Renderer;

namespace YtPlayer;

/// <summary>Draws the strip and border around the web view: the buttons, and which one the pointer is
/// over. Everything the window shows besides the page itself.</summary>
internal sealed class PipChrome(VkRenderer renderer)
{
    public static readonly RGBAColor32 Background = new(0x18, 0x18, 0x1c, 0xff);
    private static readonly RGBAColor32 Hover = new(0x33, 0x33, 0x3a, 0xff);
    private static readonly RGBAColor32 CloseHover = new(0xc4, 0x2b, 0x1c, 0xff);
    private static readonly RGBAColor32 Ink = new(0xd8, 0xd8, 0xdc, 0xff);
    private static readonly RGBAColor32 Accent = new(0xff, 0x30, 0x30, 0xff);

    public PipButton Hovered { get; set; }

    /// <param name="compact">Compact is on (the user's choice).</param>
    /// <param name="watching">The page is a watch page, so compact, if on, is showing.</param>
    public void Draw(PipLayout layout, bool compact, bool watching, bool onTop)
    {
        if (layout.Width <= 0)
            return;
        // Window points to the renderer's pixels.
        var scale = renderer.Width / (float)layout.Width;

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
            var u = 5 * scale; // half the size of a mark
            var t = Math.Max(1, (int)MathF.Round(1.5f * scale));
            switch (button)
            {
                case PipButton.Back:
                    renderer.DrawLine(cx + u / 2, cy - u, cx - u / 2, cy, Ink, t);
                    renderer.DrawLine(cx - u / 2, cy, cx + u / 2, cy + u, Ink, t);
                    break;
                case PipButton.Compact:
                    // A screen; in compact mode with the small picture filled, showing what is on.
                    // Compact on away from a video only outlines it: it waits for the next video.
                    renderer.DrawRectangle(Rect(cx - u * 1.3f, cy - u, u * 2.6f, u * 2), Ink, t);
                    var picture = Rect(cx, cy, u * 1.1f, u * 0.8f);
                    if (compact && watching)
                        renderer.FillRectangle(picture, Accent);
                    else if (compact)
                        renderer.DrawRectangle(picture, Accent, t);
                    break;
                case PipButton.Pin:
                    // A dot: filled while the window stays on top.
                    var dot = Rect(cx - u * 0.8f, cy - u * 0.8f, u * 1.6f, u * 1.6f);
                    if (onTop)
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
    }

    // RectInt takes (LowerRight, UpperLeft).
    private static RectInt Rect(float x, float y, float w, float h)
        => new(new PointInt((int)MathF.Round(x + w), (int)MathF.Round(y + h)), new PointInt((int)MathF.Round(x), (int)MathF.Round(y)));
}
