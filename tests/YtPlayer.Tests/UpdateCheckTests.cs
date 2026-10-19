using System.Text.Json;
using Shouldly;

namespace YtPlayer.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("0.1.12", "v0.1.13", true)]
    [InlineData("0.1.12", "v0.2.0", true)]
    [InlineData("0.1.12", "v0.1.12", false)]
    [InlineData("0.1.12", "v0.1.11", false)]
    [InlineData("0.1.9", "v0.1.10", true)] // numbers, not text
    [InlineData("0.1.12+3969354abc", "v0.1.12", false)] // the commit the SDK appends
    [InlineData("0.1.12", "v0.1.13-rc1", true)]
    [InlineData("1.0.0", "v0.1.13", false)] // a build not made from a tag
    [InlineData("0.1.12", "nightly", false)]
    public void IsNewerComparesVersions(string current, string latest, bool newer)
        => UpdateCheck.IsNewer(current, latest).ShouldBe(newer);

    [Fact]
    public void TheLatestReleaseIsItsTagAndPage()
        => UpdateCheck.ParseLatest("""
                {"tag_name":"v0.1.13","html_url":"https://github.com/sebgod/YtPlayer/releases/tag/v0.1.13","draft":false}
                """)
            .ShouldBe(new Release("0.1.13", "https://github.com/sebgod/YtPlayer/releases/tag/v0.1.13"));

    [Theory]
    [InlineData("""{"tag_name":"v0.1.13","html_url":"https://example.com/releases/tag/v0.1.13"}""")] // not ours
    [InlineData("""{"message":"API rate limit exceeded"}""")]
    [InlineData("""[]""")]
    [InlineData("""not json""")]
    public void AnythingElseIsNoRelease(string json) => UpdateCheck.ParseLatest(json).ShouldBeNull();

    [Fact]
    public void TheHelpMessageCarriesWhatThePanelShows()
    {
        using var doc = JsonDocument.Parse(Program.HelpMessage(open: true, "0.1.12", "0.1.13", "newer", summon: true));
        var help = doc.RootElement.GetProperty("help");
        help.GetProperty("open").GetBoolean().ShouldBeTrue();
        help.GetProperty("version").GetString().ShouldBe("0.1.12");
        help.GetProperty("newer").GetString().ShouldBe("0.1.13");
        help.GetProperty("update").GetString().ShouldBe("newer");
        help.GetProperty("summon").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void WithNoNewerReleaseTheMessageSaysNull()
    {
        using var doc = JsonDocument.Parse(Program.HelpMessage(open: false, "0.1.13", null, "current", summon: false));
        doc.RootElement.GetProperty("help").GetProperty("newer").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
