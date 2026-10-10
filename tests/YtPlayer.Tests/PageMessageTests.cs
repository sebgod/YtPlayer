using Shouldly;

namespace YtPlayer.Tests;

public class PageMessageTests
{
    [Fact]
    public void ReadyAsksForTheCompactState()
        => PageMessage.Parse("""{"ready":true}""").ShouldBe(new PageMessage(true, null, null));

    [Theory]
    [InlineData("""{"showing":true}""", true)]
    [InlineData("""{"showing":false}""", false)]
    public void ShowingSaysWhetherCompactIsOnScreen(string json, bool showing)
        => PageMessage.Parse(json).ShouldBe(new PageMessage(false, showing, null));

    [Fact]
    public void APasteCarriesItsText()
        => PageMessage.Parse("""{"paste":"https://youtu.be/dQw4w9WgXcQ"}""")
            .ShouldBe(new PageMessage(false, null, "https://youtu.be/dQw4w9WgXcQ"));

    [Fact]
    public void ACopyCarriesTheLink()
        => PageMessage.Parse("""{"copy":"https://youtu.be/dQw4w9WgXcQ?t=83"}""")
            .ShouldBe(new PageMessage(false, null, null, "https://youtu.be/dQw4w9WgXcQ?t=83"));

    [Fact]
    public void ASkipAtCarriesWhereTheButtonIs()
        => PageMessage.Parse("""{"skipAt":{"x":556.72,"y":279.33}}""").SkipAt.ShouldBe((556.72, 279.33));

    [Theory]
    [InlineData("not json")]
    [InlineData("\"a string\"")]
    [InlineData("""{"navigated":"https://www.youtube.com/"}""")]
    [InlineData("""{"showing":"yes","paste":42,"copy":false}""")]
    [InlineData("""{"skipAt":{"x":"1","y":2}}""")]
    [InlineData("""{"skipAt":[1,2]}""")]
    public void AnythingElseReadsAsEmpty(string json) => PageMessage.Parse(json).ShouldBe(default);
}
