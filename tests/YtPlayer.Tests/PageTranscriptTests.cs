using System.Text.Json;
using Shouldly;

namespace YtPlayer.Tests;

public class PageTranscriptTests
{
    private const string Zoo = """
        {"transcript":{"id":"jNQXAC9IVRw","title":"Me at the zoo","channel":"jawed","language":"English","lines":[
          {"t":0,"chapter":"Intro"},
          {"t":1,"text":"All right, so here we are, in front of the elephants"},
          {"t":5,"chapter":"The cool thing"},
          {"t":5,"text":"the cool thing about these guys is that they have really..."},
          {"t":7,"text":"really really long trunks"}]}}
        """;

    [Fact]
    public void ThePagesTranscriptCarriesTheVideoAndItsLines()
    {
        var transcript = PageMessage.Parse(Zoo).Transcript.ShouldNotBeNull();
        (transcript.Id, transcript.Title, transcript.Channel, transcript.Language)
            .ShouldBe(("jNQXAC9IVRw", "Me at the zoo", "jawed", "English"));
        transcript.Lines.ShouldBe([
            new TranscriptLine(0, "Intro", Chapter: true),
            new TranscriptLine(1, "All right, so here we are, in front of the elephants"),
            new TranscriptLine(5, "The cool thing", Chapter: true),
            new TranscriptLine(5, "the cool thing about these guys is that they have really..."),
            new TranscriptLine(7, "really really long trunks"),
        ]);
    }

    [Fact]
    public void TheFileListsTheVideoThenEachLineAtItsTime()
        => PageMessage.Parse(Zoo).Transcript!.ToText().ShouldBe(
            "Me at the zoo\n" +
            "jawed\n" +
            "https://youtu.be/jNQXAC9IVRw\n" +
            "English\n" +
            "\n" +
            "Intro\n" +
            "0:01 All right, so here we are, in front of the elephants\n" +
            "\n" +
            "The cool thing\n" +
            "0:05 the cool thing about these guys is that they have really...\n" +
            "0:07 really really long trunks\n");

    [Fact]
    public void WithNoChaptersTheLinesFollowTheHeadingAfterAnEmptyLine()
        => new PageTranscript("jNQXAC9IVRw", "A talk", "", "", [new(10, "[music]"), new(3725, "the end")]).ToText()
            .ShouldBe("A talk\nhttps://youtu.be/jNQXAC9IVRw\n\n0:10 [music]\n1:02:05 the end\n");

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(754, "12:34")]
    [InlineData(3723, "1:02:03")]
    [InlineData(36000, "10:00:00")]
    public void ATimeReadsAsYouTubeWritesIt(int seconds, string clock) => PageTranscript.Clock(seconds).ShouldBe(clock);

    [Theory]
    [InlineData("Me at the zoo", "Me at the zoo [jNQXAC9IVRw].txt")]
    // What a file's name may not hold, on Windows the least forgiving, gives way to a space.
    [InlineData("AC/DC: \"Thunderstruck\" <live> | what? *really*", "AC DC Thunderstruck live what really [jNQXAC9IVRw].txt")]
    [InlineData(@"..\..\Windows\system32\evil", @".. .. Windows system32 evil [jNQXAC9IVRw].txt")]
    [InlineData("line\r\nbreak\tand   spaces ", "line break and spaces [jNQXAC9IVRw].txt")]
    [InlineData("ends in dots...", "ends in dots [jNQXAC9IVRw].txt")]
    [InlineData("", "Transcript [jNQXAC9IVRw].txt")]
    [InlineData("???", "Transcript [jNQXAC9IVRw].txt")]
    [InlineData("CON", "CON [jNQXAC9IVRw].txt")]
    public void TheFilesNameIsTheTitleMadeSafeAndTheId(string title, string name)
        => new PageTranscript("jNQXAC9IVRw", title, "", "", [new(0, "hello")]).FileName().ShouldBe(name);

    [Fact]
    public void ALongTitleIsCutAndNoCharacterInTwo()
    {
        // 119 letters and then a character of two UTF-16 units, which the cut at 120 would halve.
        var name = new PageTranscript("jNQXAC9IVRw", new string('a', 119) + "\U0001F600 and more", "", "", [new(0, "hello")]).FileName();
        name.ShouldBe(new string('a', 119) + " [jNQXAC9IVRw].txt");
        new PageTranscript("jNQXAC9IVRw", new string('b', 300), "", "", [new(0, "hello")]).FileName()
            .ShouldBe(new string('b', 120) + " [jNQXAC9IVRw].txt");
    }

    [Fact]
    public void TextIsKeptToOneLine()
    {
        var transcript = PageMessage.Parse("""
            {"transcript":{"id":"jNQXAC9IVRw","title":"Two\nlines","lines":[{"t":3,"text":"said over\n two  lines"}]}}
            """).Transcript.ShouldNotBeNull();
        transcript.Title.ShouldBe("Two lines");
        transcript.Lines.ShouldBe([new TranscriptLine(3, "said over two lines")]);
        (transcript.Channel, transcript.Language).ShouldBe(("", ""));
    }

    [Fact]
    public void ALineThatIsNoLineIsLeftOut()
        => PageMessage.Parse("""
            {"transcript":{"id":"jNQXAC9IVRw","title":"T","lines":[
              {"t":1,"text":"kept"}, {"t":-1,"text":"before the start"}, {"t":"2","text":"no number"}, {"text":"no time"},
              {"t":3}, {"t":4,"text":""}, {"t":5,"text":42}, "a string", null, {"t":6,"chapter":"Kept too"}]}}
            """).Transcript!.Lines.ShouldBe([new TranscriptLine(1, "kept"), new TranscriptLine(6, "Kept too", Chapter: true)]);

    [Theory]
    [InlineData("""{"transcript":null}""")]
    [InlineData("""{"transcript":"some text"}""")]
    [InlineData("""{"transcript":{"title":"No id","lines":[{"t":1,"text":"hello"}]}}""")]
    // An id is eleven of YouTube's characters: nothing that could leave the folder, or name another file.
    [InlineData("""{"transcript":{"id":"../../../ab","title":"T","lines":[{"t":1,"text":"hello"}]}}""")]
    [InlineData("""{"transcript":{"id":"jNQXAC9IVRw","title":"No lines"}}""")]
    [InlineData("""{"transcript":{"id":"jNQXAC9IVRw","title":"No lines","lines":[]}}""")]
    [InlineData("""{"transcript":{"id":"jNQXAC9IVRw","title":"No lines","lines":[{"t":1}]}}""")]
    public void AnythingElseIsNoTranscript(string json) => PageMessage.Parse(json).Transcript.ShouldBeNull();

    [Fact]
    public void TheAnswerNamesTheFileAndItsFolder()
    {
        using var doc = JsonDocument.Parse(Program.TranscriptSavedMessage("Downloads", "A \"quoted\" title [jNQXAC9IVRw].txt"));
        var saved = doc.RootElement.GetProperty("transcriptSaved");
        saved.GetProperty("folder").GetString().ShouldBe("Downloads");
        saved.GetProperty("file").GetString().ShouldBe("A \"quoted\" title [jNQXAC9IVRw].txt");
    }

    [Fact]
    public void TheAnswerIsNullWhenNothingWasWritten()
        => Program.TranscriptSavedMessage("Downloads", null).ShouldBe("""{"transcriptSaved":null}""");
}
