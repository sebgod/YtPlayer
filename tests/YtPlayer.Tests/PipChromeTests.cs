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
    [InlineData(14, 14, PipButton.Back)]
    [InlineData(42, 14, PipButton.Home)]
    [InlineData(76, 14, PipButton.Previous)] // past the 6 between the page's buttons and the video's
    [InlineData(104, 14, PipButton.PlayPause)]
    [InlineData(132, 14, PipButton.Next)]
    [InlineData(298, 14, PipButton.Collapse)]
    [InlineData(326, 14, PipButton.Compact)]
    [InlineData(354, 14, PipButton.Pin)]
    [InlineData(382, 14, PipButton.Help)]
    [InlineData(410, 14, PipButton.Hide)]
    [InlineData(438, 14, PipButton.Fullscreen)]
    [InlineData(466, 14, PipButton.Close)]
    public void EachButtonIsWhereItIsDrawn(float x, float y, PipButton expected)
        => Chrome.HitTest(x, y).ShouldBe(new HitResult.ButtonHit(expected.ToString()));

    [Theory]
    [InlineData(200, 14)] // the strip between the buttons
    [InlineData(59, 14)] // between home and previous
    [InlineData(465, 100)] // the page, under the close button
    public void NoButtonIsElsewhere(float x, float y)
        => Chrome.HitTest(x, y).ShouldNotBeOfType<HitResult.ButtonHit>();

    [Fact]
    public void AButtonIsNotADragHandle()
    {
        foreach (var x in new[] { 14, 42, 76, 104, 132, 298, 326, 354, 382, 410, 438, 466 })
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
        new PipChrome<RgbaImage>(renderer) { Pointer = (455, 14) }.Render(Plain);
        // The close button's corner, clear of its mark: the close button's red.
        Pixel(renderer, 455, 25).ShouldBe((0xc4, 0x2b, 0x1c));
        // The fullscreen button beside it is not lit.
        Pixel(renderer, 450, 25).ShouldBe((0x18, 0x18, 0x1c));
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
        title.Bounds.ShouldBe(new Rect<float>(152, 0, 126, 28)); // 146 + 6 to 284 - 6
    }

    [Fact]
    public void ATitleWithNoRoomIsLeftOut()
    {
        var chrome = new PipChrome<RgbaImage>(new RgbaImageRenderer(368, 200)) { FontPath = "any" };
        // 368 less twelve buttons of 28, the 6 between them and the padding leaves 14, under the 24 a title needs.
        Layout.Engine.Arrange(chrome.Build(Plain with { Title = "A video" }), new Rect<float>(0, 0, 368, 200),
                new UnitContext())
            .Any(n => n.Node is Layout.Node.Leaf { Content: Layout.Content.Text })
            .ShouldBeFalse();
    }

    [Fact]
    public void TheNarrowestWindowHasRoomForEveryButton()
    {
        var width = WindowFit.PipMinimum.W;
        var chrome = Painted(width, WindowFit.PipMinimum.H, Plain);
        // Five from the left, the 6 after the second; seven from the right.
        float[] left = [14, 42, 76, 104, 132];
        float[] right = [width - 196 + 14, width - 168 + 14, width - 140 + 14, width - 112 + 14, width - 84 + 14,
            width - 56 + 14, width - 14];
        var at = left.Concat(right).ToArray();
        var buttons = Enum.GetValues<PipButton>();
        at.Length.ShouldBe(buttons.Length);
        for (var i = 0; i < buttons.Length; i++)
            chrome.HitTest(at[i], 14).ShouldBe(new HitResult.ButtonHit(buttons[i].ToString()), $"at {at[i]}");
    }

    [Fact]
    public void WithoutFullscreenHideSitsBesideClose()
    {
        var chrome = new PipChrome<RgbaImage>(new RgbaImageRenderer(480, 300)) { CanFullscreen = false };
        chrome.Render(Plain);
        chrome.HitTest(438, 14).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Hide)));
        chrome.HitTest(466, 14).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Close)));
        chrome.HitTest(410, 14).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Help)));
    }

    [Fact]
    public void WithANewerReleaseOutTheHelpButtonWearsARedDot()
    {
        // The dot's middle: at the help mark's top right (the button 368 to 396, its mark 377 to 387, from 9 down).
        var renderer = new RgbaImageRenderer(480, 300);
        new PipChrome<RgbaImage>(renderer).Render(Plain);
        Pixel(renderer, 388, 9).ShouldBe((0x18, 0x18, 0x1c));
        new PipChrome<RgbaImage>(renderer).Render(Plain with { Update = true });
        Pixel(renderer, 388, 9).ShouldBe((0xff, 0x30, 0x30));
    }

    private static readonly ChromeState Folded = Plain with { Mode = PipMode.Collapsed };
    private static readonly PipChrome<RgbaImage> Collapsed = Painted(480, 32, Folded);

    [Fact]
    public void CollapsedThereIsNoWebView() => Collapsed.WebViewBounds(Folded, 480, 32).ShouldBe(default);

    [Fact]
    public void CollapsedTheWindowIsTheStripAndTheBorderUnderIt() => Collapsed.CollapsedHeight().ShouldBe(32f);

    [Theory]
    [InlineData(200, 14, HitTestResult.Draggable)]
    [InlineData(200, 1, HitTestResult.Draggable)] // the top edge, which resizes when not collapsed
    [InlineData(478, 30, HitTestResult.Draggable)]
    [InlineData(200, 31, HitTestResult.Draggable)]
    [InlineData(465, 2, HitTestResult.Normal)] // the close button, right up to the top edge
    public void CollapsedTheStripDragsAndNothingResizes(float x, float y, HitTestResult expected)
        => Collapsed.FrameAreaAt(x, y).ShouldBe(expected);

    // At 150% (144 dpi) every design unit is a pixel and a half: the strip 42 pixels tall, a button 42 wide.
    private static readonly PipChrome<RgbaImage> Scaled = Painted(720, 450, Plain, scale: 1.5f);

    [Fact]
    public void ScaledTheStripAndBorderGrowWithTheDisplay()
    {
        Scaled.WebViewBounds(Plain, 720, 450).ShouldBe(Rect(6, 42, 708, 402));
        Scaled.HitTest(670, 1).ShouldBeOfType<FrameHit>(); // the top edge, 6 pixels deep
        Scaled.HitTest(690, 7).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Close)));
        Scaled.HitTest(677, 20).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Fullscreen)));
        Scaled.HitTest(678, 20).ShouldBe(new HitResult.ButtonHit(nameof(PipButton.Close))); // 720 - 42
        Scaled.CollapsedHeight().ShouldBe(32f); // design units: the window scales it to its own coordinates
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
