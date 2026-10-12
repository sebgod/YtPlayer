using DIR.Lib;
using Shouldly;
using static SDL3.SDL;
using Layout = DIR.Lib.Layout;

namespace YtPlayer.Tests;

// The chrome painted into DIR.Lib's software renderer, as the window paints it: what is asked of it below is what
// the painter registered, the same regions SDL's hit test and the router read.
public class PipChromeTests
{
    private static readonly ChromeState Plain = new(PipMode.Normal, Active: false, Compact: false, Showing: false,
        OnTop: false, Playing: false, Title: null);

    private static PipChrome<RgbaImage> Painted(int width, int height, ChromeState state, float scale = 1)
    {
        var chrome = new PipChrome<RgbaImage>(new RgbaImageRenderer((uint)width, (uint)height)) { DpiScale = scale };
        chrome.Render(state);
        return chrome;
    }

    private static readonly PipChrome<RgbaImage> Chrome = Painted(480, 300, Plain);

    private static RectInt Rect(int x, int y, int w, int h) => new(new PointInt(x + w, y + h), new PointInt(x, y));

    [Fact]
    public void TheWebViewFillsAllButTheStripAndBorder()
        => Chrome.WebViewBounds(Plain, 480, 300).ShouldBe(Rect(4, 28, 472, 268));

    [Theory]
    [InlineData(200, 14, HitTestResult.Draggable)]
    [InlineData(200, 150, HitTestResult.Normal)]
    [InlineData(1, 150, HitTestResult.ResizeLeft)]
    [InlineData(478, 150, HitTestResult.ResizeRight)]
    [InlineData(200, 1, HitTestResult.ResizeTop)]
    [InlineData(200, 298, HitTestResult.ResizeBottom)]
    [InlineData(1, 1, HitTestResult.ResizeTopLeft)]
    [InlineData(10, 1, HitTestResult.ResizeTopLeft)]
    [InlineData(478, 298, HitTestResult.ResizeBottomRight)]
    [InlineData(478, 290, HitTestResult.ResizeBottomRight)]
    [InlineData(1, 298, HitTestResult.ResizeBottomLeft)]
    [InlineData(478, 5, HitTestResult.ResizeTopRight)]
    [InlineData(455, 2, HitTestResult.ResizeTop)] // the top edge, above the close button
    public void TheEdgesResizeAndTheStripDrags(float x, float y, HitTestResult expected)
        => Chrome.FrameAreaAt(x, y).ShouldBe(expected);

    [Theory]
    [InlineData(17, 14, PipButton.Back)]
    [InlineData(50, 14, PipButton.PlayPause)]
    [InlineData(85, 14, PipButton.Next)]
    [InlineData(360, 14, PipButton.Collapse)]
    [InlineData(400, 14, PipButton.Compact)]
    [InlineData(430, 14, PipButton.Pin)]
    [InlineData(465, 14, PipButton.Close)]
    public void EachButtonIsWhereItIsDrawn(float x, float y, PipButton expected)
        => Chrome.HitTest(x, y).ShouldBe(new HitResult.ButtonHit(expected.ToString()));

    [Theory]
    [InlineData(200, 14)] // the strip between the buttons
    [InlineData(465, 100)] // the page, under the close button
    public void NoButtonIsElsewhere(float x, float y)
        => Chrome.HitTest(x, y).ShouldNotBeOfType<HitResult.ButtonHit>();

    [Fact]
    public void AButtonIsNotADragHandle()
    {
        foreach (var x in new[] { 17, 50, 85, 360, 400, 430, 465 })
            Chrome.FrameAreaAt(x, 14).ShouldBe(HitTestResult.Normal);
    }

    [Fact]
    public void APressOnAButtonReachesTheHost()
    {
        var chrome = Painted(480, 300, Plain);
        PipButton? pressed = null;
        chrome.Pressed = b => pressed = b;
        IPixelWidget[] widgets = [chrome];
        var router = new InputRouter(chrome.Ui, new BackgroundTaskTracker(), () => { }) { Widgets = () => widgets };

        router.Handle(new InputEvent.MouseDown(465, 14)).ShouldBeTrue();

        pressed.ShouldBe(PipButton.Close);
    }

    [Fact]
    public void TheButtonUnderThePointerLights()
    {
        var renderer = new RgbaImageRenderer(480, 300);
        new PipChrome<RgbaImage>(renderer) { Pointer = (450, 14) }.Render(Plain);
        // The close button's corner, clear of its mark: the close button's red.
        Pixel(renderer, 450, 25).ShouldBe((0xc4, 0x2b, 0x1c));
        // The pin button beside it is not lit.
        Pixel(renderer, 415, 25).ShouldBe((0x18, 0x18, 0x1c));
    }

    [Fact]
    public void TheActiveWindowsStripIsLighter()
    {
        var renderer = new RgbaImageRenderer(480, 300);
        new PipChrome<RgbaImage>(renderer).Render(Plain with { Active = true });
        Pixel(renderer, 200, 14).ShouldBe((0x3a, 0x3a, 0x4a));
    }

    [Fact]
    public void TheTitleSitsBetweenTheButtons()
    {
        var chrome = new PipChrome<RgbaImage>(new RgbaImageRenderer(480, 300)) { FontPath = "any" };
        var arranged = Layout.Engine.Arrange(chrome.Build(Plain with { Title = "A video" }),
            new Rect<float>(0, 0, 480, 300), new UnitContext());
        var title = arranged.Single(n => n.Node is Layout.Node.Leaf { Content: Layout.Content.Text });
        title.Bounds.ShouldBe(new Rect<float>(108, 0, 230, 28)); // 102 + 6 to 344 - 6
    }

    [Fact]
    public void ATitleWithNoRoomIsLeftOut()
    {
        var chrome = new PipChrome<RgbaImage>(new RgbaImageRenderer(260, 200)) { FontPath = "any" };
        // 260 less seven buttons of 34 and the padding leaves 10, under the 24 a title needs.
        Layout.Engine.Arrange(chrome.Build(Plain with { Title = "A video" }), new Rect<float>(0, 0, 260, 200),
                new UnitContext())
            .Any(n => n.Node is Layout.Node.Leaf { Content: Layout.Content.Text })
            .ShouldBeFalse();
    }

    private static readonly ChromeState Folded = Plain with { Mode = PipMode.Collapsed };
    private static readonly PipChrome<RgbaImage> Collapsed = Painted(480, 32, Folded);

    [Fact]
    public void CollapsedThereIsNoWebView() => Collapsed.WebViewBounds(Folded, 480, 32).ShouldBe(default);

    [Fact]
    public void CollapsedTheWindowIsTheStripAndTheBorderUnderIt() => Collapsed.CollapsedHeight().ShouldBe(32);

    [Theory]
    [InlineData(200, 14, HitTestResult.Draggable)]
    [InlineData(200, 1, HitTestResult.Draggable)] // the top edge, which resizes when not collapsed
    [InlineData(478, 30, HitTestResult.Draggable)]
    [InlineData(200, 31, HitTestResult.Draggable)]
    [InlineData(465, 2, HitTestResult.Normal)] // the close button, right up to the top edge
    public void CollapsedTheStripDragsAndNothingResizes(float x, float y, HitTestResult expected)
        => Collapsed.FrameAreaAt(x, y).ShouldBe(expected);

    // At 150% (144 dpi) every design unit is a pixel and a half: the strip 42 pixels tall, a button 51 wide.
    private static readonly PipChrome<RgbaImage> Scaled = Painted(720, 450, Plain, scale: 1.5f);

    [Fact]
    public void ScaledTheStripAndBorderGrowWithTheDisplay()
    {
        Scaled.WebViewBounds(Plain, 720, 450).ShouldBe(Rect(6, 42, 708, 402));
        Scaled.HitTest(670, 1).ShouldBeOfType<FrameHit>(); // the top edge, 6 pixels deep
        Scaled.HitTest(670, 7).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Close)));
        Scaled.HitTest(668, 20).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Pin)));
        Scaled.HitTest(669, 20).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Close))); // 720 - 51
        Scaled.CollapsedHeight().ShouldBe(48);
    }

    [Theory]
    [InlineData(5, 200, HitTestResult.ResizeLeft)] // 4 pixels in would be the page at 100%
    [InlineData(360, 40, HitTestResult.Draggable)] // below a 28-pixel strip
    [InlineData(700, 40, HitTestResult.Normal)] // the close button
    public void ScaledTheHitTestUsesTheScaledSizes(float x, float y, HitTestResult expected)
        => Scaled.FrameAreaAt(x, y).ShouldBe(expected);

    // Fullscreen, the page is the whole window: no strip, no border, nothing to drag or resize.
    private static readonly ChromeState Full = Plain with { Mode = PipMode.Fullscreen };
    private static readonly PipChrome<RgbaImage> Fullscreen = Painted(2880, 1920, Full, scale: 1.5f);

    [Fact]
    public void FullscreenThePageIsTheWholeWindow()
        => Fullscreen.WebViewBounds(Full, 2880, 1920).ShouldBe(Rect(0, 0, 2880, 1920));

    [Theory]
    [InlineData(1, 1)] // a corner, which resizes otherwise
    [InlineData(1440, 20)] // the strip, which drags otherwise
    [InlineData(2860, 20)] // the close button
    public void FullscreenNothingDragsResizesOrIsAButton(float x, float y)
    {
        Fullscreen.FrameAreaAt(x, y).ShouldBe(HitTestResult.Normal);
        Fullscreen.HitTest(x, y).ShouldBeNull();
    }

    private static (int R, int G, int B) Pixel(RgbaImageRenderer renderer, int x, int y)
    {
        var surface = renderer.Surface;
        var i = (y * surface.Width + x) * 4;
        return (surface.Pixels[i], surface.Pixels[i + 1], surface.Pixels[i + 2]);
    }

    // Text as wide as its characters times its size, and design units as pixels: geometry without a font.
    private sealed class UnitContext : Layout.IMeasureContext<float>
    {
        public Layout.Size<float> MeasureText(ReadOnlySpan<char> text, float fontSize) => new(text.Length * fontSize, fontSize);

        public float ToSurface(float designUnits) => designUnits;
    }
}
