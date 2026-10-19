using SdlVulkan.Renderer;
using Shouldly;

namespace YtPlayer.Tests;

public class PageMessageTests
{
    [Fact]
    public void F1AsksForTheHelpPanel() => PageMessage.Parse("""{"help":true}""").Help.ShouldBeTrue();

    [Fact]
    public void TheHelpPanelsLinkAsksForTheRelease()
        => PageMessage.Parse("""{"openRelease":true}""").ShouldBe(new PageMessage(false, null, null, OpenRelease: true));

    [Fact]
    public void TheUpdateButtonCarriesWhatPlays()
    {
        var message = PageMessage.Parse("""{"update":true,"resume":"https://youtu.be/dQw4w9WgXcQ?t=83"}""");
        message.Update.ShouldBeTrue();
        message.Resume.ShouldBe("https://youtu.be/dQw4w9WgXcQ?t=83");
        message.ResumePaused.ShouldBeFalse();
        PageMessage.Parse("""{"update":true,"resume":"https://youtu.be/dQw4w9WgXcQ","paused":true}""").ResumePaused
            .ShouldBeTrue();
    }

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
    [InlineData("""{"playing":true}""", true)]
    [InlineData("""{"playing":false}""", false)]
    public void PlayingSaysWhetherTheVideoPlays(string json, bool playing)
        => PageMessage.Parse(json).Playing.ShouldBe(playing);

    [Theory]
    [InlineData("""{"collapse":true}""", true)]
    [InlineData("""{"collapse":false}""", false)]
    public void CollapseFoldsOrUnfoldsTheWindow(string json, bool fold)
        => PageMessage.Parse(json).Collapse.ShouldBe(fold);

    [Fact]
    public void QueueClipboardAsksForTheClipboardsVideo()
        => PageMessage.Parse("""{"queueClipboard":true}""").QueueClipboard.ShouldBeTrue();

    [Fact]
    public void HideAsksForTheWindowToHide() => PageMessage.Parse("""{"hide":true}""").Hide.ShouldBeTrue();

    [Fact]
    public void AnythingElseDoesNotHide() => PageMessage.Parse("""{"playing":true}""").Hide.ShouldBeFalse();

    [Theory]
    [InlineData("""{"ad":true}""", true)]
    [InlineData("""{"ad":false}""", false)]
    public void AdSaysWhenAnAdStartsAndEnds(string json, bool ad) => PageMessage.Parse(json).Ad.ShouldBe(ad);

    [Fact]
    public void MediaCarriesTheTrack()
        => PageMessage.Parse("""
            {"media":{"title":"Big Buck Bunny","artist":"Blender","artwork":"https://i.ytimg.com/vi/aqz-KE-bpKQ/hqdefault.jpg","previous":false,"next":true}}
            """).Media.ShouldBe(new PageTrack("Big Buck Bunny", "Blender", "https://i.ytimg.com/vi/aqz-KE-bpKQ/hqdefault.jpg", false, true));

    [Fact]
    public void MediaWithoutArtworkOrArtistStillCarriesTheTitle()
        => PageMessage.Parse("""{"media":{"title":"Big Buck Bunny","artwork":null}}""").Media
            .ShouldBe(new PageTrack("Big Buck Bunny", "", null, false, false));

    [Fact]
    public void MediaNullTakesTheTrackAway()
    {
        var message = PageMessage.Parse("""{"media":null}""");
        message.MediaGone.ShouldBeTrue();
        message.Media.ShouldBeNull();
    }

    [Fact]
    public void AnythingElseLeavesTheTrack()
        => PageMessage.Parse("""{"playing":true}""").ShouldBe(new PageMessage(false, null, null, Playing: true));

    [Theory]
    [InlineData(MediaButton.Play, "play")]
    [InlineData(MediaButton.Pause, "pause")]
    [InlineData(MediaButton.Stop, "stop")]
    [InlineData(MediaButton.Next, "nexttrack")]
    [InlineData(MediaButton.Previous, "previoustrack")]
    public void APressGoesToThePageAsTheBrowsersAction(MediaButton button, string action)
        => Program.MediaActionMessage(button).ShouldBe($$"""{"mediaAction":"{{action}}"}""");

    [Theory]
    [InlineData("not json")]
    [InlineData("\"a string\"")]
    [InlineData("""{"navigated":"https://www.youtube.com/"}""")]
    [InlineData("""{"showing":"yes","paste":42,"copy":false}""")]
    [InlineData("""{"skipAt":{"x":"1","y":2}}""")]
    [InlineData("""{"skipAt":[1,2]}""")]
    [InlineData("""{"media":{"title":""}}""")]
    [InlineData("""{"media":"a track"}""")]
    public void AnythingElseReadsAsEmpty(string json) => PageMessage.Parse(json).ShouldBe(default);
}
