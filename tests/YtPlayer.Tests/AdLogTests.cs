using Shouldly;

namespace YtPlayer.Tests;

public class AdLogTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 10, 8, 9);

    [Fact]
    public void ASkippedAdEndsSoonAfterTheClick()
    {
        var log = new AdLog();
        log.Started(T0).ShouldBe("ad started");
        log.SkipOffered(T0.AddSeconds(11), 556.7, 279.3, canClick: true)
            .ShouldBe("ad: Skip offered after 11.0 s, clicked at (557, 279)");
        log.Ended(T0.AddSeconds(11.4)).ShouldBe("ad ended after 11.4 s: 0.4 s after the last of 1 Skip click");
    }

    [Fact]
    public void AnAdWithNoSkipPlaysOut()
    {
        var log = new AdLog();
        log.Started(T0);
        log.Ended(T0.AddSeconds(15)).ShouldBe("ad ended after 15.0 s: played out, no Skip clicked");
    }

    [Fact]
    public void RepeatedClicksAreCounted()
    {
        var log = new AdLog();
        log.Started(T0);
        log.SkipOffered(T0.AddSeconds(5), 10, 10, canClick: true);
        log.SkipOffered(T0.AddSeconds(6), 10, 10, canClick: true).ShouldEndWith("(click 2)");
        log.Ended(T0.AddSeconds(6.2)).ShouldBe("ad ended after 6.2 s: 0.2 s after the last of 2 Skip clicks");
    }

    [Fact]
    public void WithoutATrustedClickItSaysSo()
    {
        var log = new AdLog();
        log.Started(T0);
        log.SkipOffered(T0.AddSeconds(5), 10, 10, canClick: false).ShouldContain("no trusted click");
        log.Ended(T0.AddSeconds(30)).ShouldBe("ad ended after 30.0 s: played out, no Skip clicked");
    }

    [Fact]
    public void AnEndWithNoStartIsNotLogged() => new AdLog().Ended(T0).ShouldBeNull();

    [Fact]
    public void AStartOverAnUnfinishedAdSaysSo()
    {
        var log = new AdLog();
        log.Started(T0);
        log.Started(T0.AddSeconds(30)).ShouldBe("ad started (the previous ad never reported an end)");
    }
}
