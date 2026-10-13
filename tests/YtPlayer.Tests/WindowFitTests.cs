using Shouldly;

namespace YtPlayer.Tests;

public class WindowFitTests
{
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
}
