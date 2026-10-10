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
    [InlineData(400, 14, PipButton.Compact)]
    [InlineData(430, 14, PipButton.Pin)]
    [InlineData(465, 14, PipButton.Close)]
    [InlineData(200, 14, PipButton.None)]
    [InlineData(465, 100, PipButton.None)]
    public void EachButtonIsWhereItIsDrawn(float x, float y, PipButton expected)
        => Layout.ButtonAt(x, y).ShouldBe(expected);

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
