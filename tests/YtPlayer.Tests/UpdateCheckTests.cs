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
    {
        var release = UpdateCheck.ParseLatest("""
            {"tag_name":"v0.1.13","html_url":"https://github.com/sebgod/YtPlayer/releases/tag/v0.1.13","draft":false}
            """).ShouldNotBeNull();
        release.Version.ShouldBe("0.1.13");
        release.Url.ShouldBe("https://github.com/sebgod/YtPlayer/releases/tag/v0.1.13");
        release.Assets.ShouldBeEmpty();
    }

    [Fact]
    public void ItsDownloadsCarryTheirSha256AndOnlyOursCount()
    {
        var release = UpdateCheck.ParseLatest("""
            {"tag_name":"v0.1.13","html_url":"https://github.com/sebgod/YtPlayer/releases/tag/v0.1.13","assets":[
              {"name":"ytplayer-0.1.13-win-arm64.zip","digest":"sha256:30FB4C05",
               "browser_download_url":"https://github.com/sebgod/YtPlayer/releases/download/v0.1.13/ytplayer-0.1.13-win-arm64.zip"},
              {"name":"ytplayer-0.1.13-win-x64.zip",
               "browser_download_url":"https://github.com/sebgod/YtPlayer/releases/download/v0.1.13/ytplayer-0.1.13-win-x64.zip"},
              {"name":"elsewhere.zip","digest":"sha256:00","browser_download_url":"https://example.com/elsewhere.zip"}]}
            """).ShouldNotBeNull();
        release.Assets.Select(a => (a.Name, a.Sha256)).ShouldBe([
            ("ytplayer-0.1.13-win-arm64.zip", "30fb4c05"),
            ("ytplayer-0.1.13-win-x64.zip", (string?)null),
        ]);
    }

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
    public void TheHelpMessageSaysWhetherThisBuildUpdatesItself()
    {
        using var doc = JsonDocument.Parse(Program.HelpMessage(open: true, "0.1.13", "0.1.14", "newer", summon: true,
            canUpdate: true));
        doc.RootElement.GetProperty("help").GetProperty("canUpdate").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void WithNoNewerReleaseTheMessageSaysNull()
    {
        using var doc = JsonDocument.Parse(Program.HelpMessage(open: false, "0.1.13", null, "current", summon: false));
        doc.RootElement.GetProperty("help").GetProperty("newer").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
