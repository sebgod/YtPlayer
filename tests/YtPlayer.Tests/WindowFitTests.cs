using DIR.Lib;
using Shouldly;

namespace YtPlayer.Tests;

public class WindowFitTests
{
    // A 1920x1080 display with a 48-px taskbar along the bottom.
    private static readonly Box Usable = new(0, 0, 1920, 1032);

    [Fact]
    public void APipParkedBottomRightGrowsUpAndLeft()
        => WindowFit.Place(new Box(1500, 760, 400, 250), (900, 750), Usable)
            .ShouldBe(new Box(1000, 260, 900, 750));

    [Fact]
    public void AWindowTopLeftGrowsDownAndRight()
        => WindowFit.Place(new Box(40, 30, 400, 250), (900, 750), Usable)
            .ShouldBe(new Box(40, 30, 900, 750));

    [Fact]
    public void ShrinkingBackKeepsTheSameCorner()
        => WindowFit.Place(new Box(1000, 260, 900, 750), (400, 250), Usable)
            .ShouldBe(new Box(1500, 760, 400, 250));

    [Fact]
    public void AWindowTopRightGrowsDownAndLeft()
        => WindowFit.Place(new Box(1700, 20, 200, 150), (900, 750), Usable)
            .ShouldBe(new Box(1000, 20, 900, 750));

    [Fact]
    public void AWindowPartlyOffTheDisplayIsPulledBackOn()
        => WindowFit.Place(new Box(-300, 900, 400, 250), (900, 750), Usable)
            .ShouldBe(new Box(0, 282, 900, 750));

    [Fact]
    public void ASizeBiggerThanTheDisplayIsCappedToIt()
        => WindowFit.Place(new Box(100, 100, 400, 250), (2500, 1400), Usable)
            .ShouldBe(new Box(0, 0, 1920, 1032));

    [Fact]
    public void LeavingAVideoKeepsTheWidthAndTakesTheBrowseHeight()
        => WindowFit.Switch(toPip: false, currentWidth: 939, height: 780, browseMinimumWidth: 840, narrowPipWidth: null)
            .ShouldBe(((939, 780), (int?)null));

    [Fact]
    public void OpeningAVideoKeepsTheWidthAndTakesThePipHeight()
        => WindowFit.Switch(toPip: true, currentWidth: 939, height: 582, browseMinimumWidth: 840, narrowPipWidth: null)
            .ShouldBe(((939, 582), (int?)null));

    [Fact]
    public void ANarrowPipWidensToBrowseAndGetsItsWidthBack()
    {
        var (browse, narrow) = WindowFit.Switch(toPip: false, 540, 780, 840, null);
        browse.ShouldBe((840, 780));
        narrow.ShouldBe(540);
        WindowFit.Switch(toPip: true, 840, 345, 840, narrow).ShouldBe(((540, 345), (int?)null));
    }

    [Fact]
    public void ScalingRoundsToWholePixels()
        => WindowFit.Scaled(WindowFit.BrowseMinimum, new DesignScale(1.5f)).ShouldBe((840, 720));

    [Fact]
    public void SecondMonitorCoordinatesWork()
        => WindowFit.Place(new Box(3500, 800, 400, 250), (900, 750), new Box(1920, 0, 2560, 1400))
            .ShouldBe(new Box(3000, 300, 900, 750));
}
