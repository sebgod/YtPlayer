using Shouldly;

namespace YtPlayer.Tests;

public class PageMessageTests
{
    [Fact]
    public void ReadyAsksForTheCompactState()
        => PageMessage.Parse("""{"ready":true,"watching":false}""").ShouldBe(new PageMessage(true, false, null));

    [Fact]
    public void ANavigationSaysWhetherItIsAWatchPage()
        => PageMessage.Parse("""{"navigated":"https://www.youtube.com/watch?v=dQw4w9WgXcQ","watching":true}""")
            .ShouldBe(new PageMessage(false, true, null));

    [Fact]
    public void APasteCarriesItsText()
        => PageMessage.Parse("""{"paste":"https://youtu.be/dQw4w9WgXcQ"}""")
            .ShouldBe(new PageMessage(false, null, "https://youtu.be/dQw4w9WgXcQ"));

    [Theory]
    [InlineData("not json")]
    [InlineData("\"a string\"")]
    [InlineData("""{"watching":"yes","paste":42}""")]
    public void AnythingElseReadsAsEmpty(string json) => PageMessage.Parse(json).ShouldBe(default);
}
