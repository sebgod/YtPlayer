using Shouldly;
using static SDL3.SDL;

namespace YtPlayer.Tests;

public class PipLayoutTests
{
    private static readonly PipLayout Layout = new(480, 300);

    [Fact]
    public void TheWebViewFillsAllButTheStripAndBorder()
        => Layout.WebView.ShouldBe(new Box(4, 28, 472, 268));

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
    public void TheEdgesResizeAndTheStripDrags(float x, float y, HitTestResult expected)
        => Layout.HitTest(x, y).ShouldBe(expected);

    [Theory]
    [InlineData(17, 14, PipButton.Back)]
    [InlineData(50, 14, PipButton.PlayPause)]
    [InlineData(360, 14, PipButton.Collapse)]
    [InlineData(400, 14, PipButton.Compact)]
    [InlineData(430, 14, PipButton.Pin)]
    [InlineData(465, 14, PipButton.Close)]
    [InlineData(200, 14, PipButton.None)]
    [InlineData(465, 100, PipButton.None)]
    public void EachButtonIsWhereItIsDrawn(float x, float y, PipButton expected)
        => Layout.ButtonAt(x, y).ShouldBe(expected);

    [Fact]
    public void TheTitleSitsBetweenTheButtons()
        => Layout.Title.ShouldBe(new Box(74, 0, 264, 28)); // 68 + 6 to 344 - 6

    private static readonly PipLayout Collapsed = new(480, PipLayout.CollapsedHeightAt(1), Collapsed: true);

    [Fact]
    public void CollapsedThereIsNoWebView() => Collapsed.WebView.ShouldBe(default);

    [Theory]
    [InlineData(200, 14, HitTestResult.Draggable)]
    [InlineData(200, 1, HitTestResult.Draggable)] // the top edge, which resizes when not collapsed
    [InlineData(478, 30, HitTestResult.Draggable)]
    [InlineData(200, 31, HitTestResult.Draggable)]
    [InlineData(465, 2, HitTestResult.Normal)] // the close button, right up to the top edge
    public void CollapsedTheStripDragsAndNothingResizes(float x, float y, HitTestResult expected)
        => Collapsed.HitTest(x, y).ShouldBe(expected);

    // At 150% (144 dpi) every logical size is half again as big: the strip 42 points tall, a button 51 wide.
    private static readonly PipLayout Scaled = new(720, 450, Scale: 1.5f);

    [Fact]
    public void ScaledTheStripAndBorderGrowWithTheDisplay()
    {
        Scaled.WebView.ShouldBe(new Box(6, 42, 708, 402));
        Scaled.ButtonBox(PipButton.Close).ShouldBe(new Box(669, 0, 51, 42));
        Scaled.Title.ShouldBe(new Box(111, 0, 396, 42)); // 102 + 9 to 516 - 9
        PipLayout.CollapsedHeightAt(1.5f).ShouldBe(48);
    }

    [Theory]
    [InlineData(5, 200, HitTestResult.ResizeLeft)] // 4 points in would be the page at 100%
    [InlineData(360, 40, HitTestResult.Draggable)] // below a 28-point strip
    [InlineData(700, 40, HitTestResult.Normal)] // the close button
    public void ScaledTheHitTestUsesTheScaledSizes(float x, float y, HitTestResult expected)
        => Scaled.HitTest(x, y).ShouldBe(expected);

    [Fact]
    public void ScaledEachButtonIsWhereItIsDrawn()
        => Scaled.ButtonAt(700, 40).ShouldBe(PipButton.Close);

    [Fact]
    public void AButtonIsNotADragHandle()
    {
        foreach (var button in PipLayout.Buttons)
        {
            var box = Layout.ButtonBox(button);
            Layout.HitTest(box.X + box.W / 2f, box.Y + box.H / 2f).ShouldBe(HitTestResult.Normal);
        }
    }
}
