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

    // The tooltips: the router says which is due, once the pointer has rested on a button for its delay.
    private static (PipChrome<RgbaImage> Chrome, InputRouter Router, FakeClock Clock) Hovering(
        int width, int height, ChromeState state, float x, float y)
    {
        var chrome = Painted(width, height, state);
        var clock = new FakeClock();
        IPixelWidget[] widgets = [chrome];
        var router = new InputRouter(chrome.Ui, new BackgroundTaskTracker(), () => { }) { Widgets = () => widgets, Clock = clock };
        router.Handle(new InputEvent.MouseMove(x, y));
        return (chrome, router, clock);
    }

    [Fact]
    public void AButtonsTooltipIsDueOnceThePointerHasRested()
    {
        var (_, router, clock) = Hovering(480, 300, Plain, 104, 14);
        router.Tooltip.ShouldBeNull();
        clock.Advance(TimeSpan.FromMilliseconds(499));
        router.Tooltip.ShouldBeNull();
        router.TooltipNeedsFrame.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        router.TooltipNeedsFrame.ShouldBeTrue(); // the loop paints a frame for it
        var due = router.Tooltip.ShouldNotBeNull();
        due.Text.ShouldBe("Play (Ctrl+P)");
        due.Anchor.ShouldBe(new RectF32(90, 0, 28, 28)); // the play/pause button
    }

    [Fact]
    public void TheTooltipFollowsWhatAPressWouldDo()
    {
        var (chrome, router, clock) = Hovering(480, 300, Plain, 104, 14);
        clock.Advance(TimeSpan.FromSeconds(1));
        router.Tooltip!.Value.Text.ShouldBe("Play (Ctrl+P)");
        // The video plays: the next paint says pause, and that tooltip waits its own delay.
        chrome.Render(Plain with { Playing = true });
        router.AfterPaint();
        router.Tooltip.ShouldBeNull();
        clock.Advance(TimeSpan.FromSeconds(1));
        router.Tooltip!.Value.Text.ShouldBe("Pause (Ctrl+P)");
    }

    [Fact]
    public void TheTooltipGoesWhenThePointerLeaves()
    {
        var (_, router, clock) = Hovering(480, 300, Plain, 104, 14);
        clock.Advance(TimeSpan.FromSeconds(1));
        router.Tooltip.ShouldNotBeNull();
        router.Handle(new InputEvent.MouseMove(200, 14)); // the strip between the buttons
        clock.Advance(TimeSpan.FromSeconds(1));
        router.Tooltip.ShouldBeNull();
    }

    [Fact]
    public void ATooltipOpensTowardsTheStripsMiddle()
    {
        foreach (var (x, side) in new[] { (14f, Layout.DockSide.Right), (132f, Layout.DockSide.Right),
                     (298f, Layout.DockSide.Left), (466f, Layout.DockSide.Left) })
        {
            var (_, router, clock) = Hovering(480, 300, Plain, x, 14);
            clock.Advance(TimeSpan.FromSeconds(1));
            router.Tooltip!.Value.Side.ShouldBe(side, $"at {x}");
        }
    }

    [Theory]
    [InlineData(PipButton.Back, "Back to the previous page")]
    [InlineData(PipButton.Home, "YouTube home; the video plays on")]
    [InlineData(PipButton.Previous, "Previous, or start over (Shift+P)")]
    [InlineData(PipButton.PlayPause, "Play (Ctrl+P)")]
    [InlineData(PipButton.Next, "Next video (Shift+N)")]
    [InlineData(PipButton.Collapse, "Fold up (Ctrl+Up)")]
    [InlineData(PipButton.Compact, "Show only the video")]
    [InlineData(PipButton.Pin, "Stay on top: off")]
    [InlineData(PipButton.Help, "Help and keys (F1)")]
    [InlineData(PipButton.Hide, "Hide; Win+/ brings it back (Ctrl+H)")]
    [InlineData(PipButton.Fullscreen, "Fullscreen (F)")]
    [InlineData(PipButton.Close, "Close YtPlayer")]
    public void ATooltipNamesTheButtonAndItsKey(PipButton button, string text)
        => PipChrome<RgbaImage>.Tooltip(button, Plain, canSummon: true).ShouldBe(text);

    [Fact]
    public void ATooltipSaysWhatAPressDoesAsThingsAre()
    {
        static string Tip(PipButton button, ChromeState state, bool canSummon = true)
            => PipChrome<RgbaImage>.Tooltip(button, state, canSummon);
        Tip(PipButton.PlayPause, Plain with { Playing = true }).ShouldBe("Pause (Ctrl+P)");
        Tip(PipButton.Collapse, Plain with { Mode = PipMode.Collapsed }).ShouldBe("Unfold (Ctrl+Down)");
        Tip(PipButton.Compact, Plain with { Compact = true, Showing = true }).ShouldBe("Show the whole page");
        Tip(PipButton.Compact, Plain with { Compact = true }).ShouldBe("Compact: on (no video)");
        Tip(PipButton.Pin, Plain with { OnTop = true }).ShouldBe("Stay on top: on");
        Tip(PipButton.Help, Plain with { Update = true }).ShouldBe("Help (F1): an update is out");
        // Without Win+/ the button only minimizes, and promises no more.
        Tip(PipButton.Hide, Plain, canSummon: false).ShouldBe("Minimize (Ctrl+H)");
    }

    // The strip is one line tall, so a tooltip is one line: where it has the room.
    [Fact]
    public void ATooltipStaysBesideItsButtonWhereOneLineFits()
    {
        var left = new TooltipRequest("Fold up", new RectF32(284, 0, 28, 28)) { Side = Layout.DockSide.Left };
        PipChrome<RgbaImage>.InStrip(left, stripWidth: 480, boxWidth: 280, margin: 2).ShouldBe(left);
        var right = new TooltipRequest("Back", new RectF32(0, 0, 28, 28)) { Side = Layout.DockSide.Right };
        PipChrome<RgbaImage>.InStrip(right, stripWidth: 480, boxWidth: 448, margin: 2).ShouldBe(right);
    }

    [Fact]
    public void ATooltipWithNoRoomOnItsSideOpensOnTheOther()
    {
        var due = new TooltipRequest("Fold up", new RectF32(152, 0, 28, 28)) { Side = Layout.DockSide.Left };
        // 148 to its left, 164 to its right.
        PipChrome<RgbaImage>.InStrip(due, stripWidth: 348, boxWidth: 160, margin: 2)
            .ShouldBe(due with { Side = Layout.DockSide.Right });
    }

    [Fact]
    public void ATooltipWithRoomOnNeitherSideRunsFromTheStripsEdge()
    {
        var opensLeft = new TooltipRequest("Fold up", new RectF32(152, 0, 28, 28)) { Side = Layout.DockSide.Left };
        PipChrome<RgbaImage>.InStrip(opensLeft, stripWidth: 348, boxWidth: 200, margin: 2)
            .ShouldBe(opensLeft with { Anchor = new RectF32(0, 0, 0, 28), Side = Layout.DockSide.Right });
        var opensRight = new TooltipRequest("Next video", new RectF32(118, 0, 28, 28)) { Side = Layout.DockSide.Right };
        PipChrome<RgbaImage>.InStrip(opensRight, stripWidth: 348, boxWidth: 300, margin: 2)
            .ShouldBe(opensRight with { Anchor = new RectF32(348, 0, 0, 28), Side = Layout.DockSide.Left });
    }

    // The wording is short enough for the narrowest window: each tooltip, whatever the state, fits beside its
    // button on the side it names, in a face as wide as the widest the player takes (a monospaced one, each
    // character 0.55 of its size: Windows' Consolas), with the box's padding, border and slack around it.
    [Fact]
    public void InTheNarrowestWindowEveryTooltipFitsBesideItsButton()
    {
        var width = WindowFit.PipMinimum.W;
        float[] at = [14, 42, 76, 104, 132, width - 196 + 14, width - 168 + 14, width - 140 + 14, width - 112 + 14,
            width - 84 + 14, width - 56 + 14, width - 14];
        var buttons = Enum.GetValues<PipButton>();
        var states =
            from playing in new[] { false, true }
            from mode in new[] { PipMode.Normal, PipMode.Collapsed }
            from compact in new[] { (false, false), (true, false), (true, true) }
            from onTop in new[] { false, true }
            from update in new[] { false, true }
            select new ChromeState(mode, Active: true, compact.Item1, compact.Item2, onTop, playing, Title: null, update);
        foreach (var state in states)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var (_, router, clock) = Hovering(width, mode: state, x: at[i]);
                clock.Advance(TimeSpan.FromSeconds(1));
                var due = router.Tooltip.ShouldNotBeNull($"{buttons[i]}");
                var box = due.Text.Length * 12 * 0.55f + 2 * (6 + 1) + 4;
                PipChrome<RgbaImage>.InStrip(due, width, box, margin: 2).ShouldBe(due, $"{buttons[i]}: \"{due.Text}\"");
            }
        }

        static (PipChrome<RgbaImage>, InputRouter, FakeClock) Hovering(int width, ChromeState mode, float x)
            => PipChromeTests.Hovering(width, mode.Mode == PipMode.Collapsed ? 32 : WindowFit.PipMinimum.H, mode, x, 14);
    }

    [Fact]
    public void WithNoFontTheStripPaintsNoTooltip()
    {
        var renderer = new RgbaImageRenderer(480, 300);
        var chrome = new PipChrome<RgbaImage>(renderer);
        chrome.Render(Plain);
        var plain = renderer.Surface.Pixels.ToArray();
        chrome.Render(Plain, new TooltipRequest("Play (Ctrl+P)", new RectF32(90, 0, 28, 28)) { Side = Layout.DockSide.Right });
        renderer.Surface.Pixels.ToArray().ShouldBe(plain);
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
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
