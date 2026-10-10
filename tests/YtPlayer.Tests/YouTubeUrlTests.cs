using Shouldly;

namespace YtPlayer.Tests;

public class YouTubeUrlTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    [InlineData("https://www.youtube.com/watch?list=PL1&v=dQw4w9WgXcQ&t=42s", "dQw4w9WgXcQ", "42s")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=10", "dQw4w9WgXcQ", "10")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    [InlineData("https://m.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    public void FindsTheVideoId(string input, string id, string? start)
    {
        YouTubeUrl.TryGetVideoId(input, out var found, out var foundStart).ShouldBeTrue();
        found.ShouldBe(id);
        foundStart.ShouldBe(start);
    }

    [Theory]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/feed/subscriptions")]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("lofi beats")]
    public void FindsNoIdWhereThereIsNone(string input)
        => YouTubeUrl.TryGetVideoId(input, out _, out _).ShouldBeFalse();

    [Fact]
    public void AShortOpensAsAnOrdinaryVideo()
        => YouTubeUrl.ToStartUrl("https://www.youtube.com/shorts/dQw4w9WgXcQ")
            .ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

    [Fact]
    public void AStartTimeIsKept()
        => YouTubeUrl.ToStartUrl("https://youtu.be/dQw4w9WgXcQ?t=10")
            .ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10");

    [Fact]
    public void NothingOpensHome() => YouTubeUrl.ToStartUrl(null).ShouldBe(YouTubeUrl.Home);

    [Fact]
    public void AnotherYouTubePageOpensAsGiven()
        => YouTubeUrl.ToStartUrl("https://www.youtube.com/feed/subscriptions")
            .ShouldBe("https://www.youtube.com/feed/subscriptions");

    [Fact]
    public void WordsAreASearch()
        => YouTubeUrl.ToStartUrl("lofi beats")
            .ShouldBe("https://www.youtube.com/results?search_query=lofi%20beats");

    [Fact]
    public void ALinkElsewhereIsASearchNotANavigation()
        => YouTubeUrl.ToStartUrl("https://example.com/").ShouldStartWith("https://www.youtube.com/results?");
}
