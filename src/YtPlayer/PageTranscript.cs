using System.Text;
using System.Text.Json;

namespace YtPlayer;

/// <summary>A line of a transcript: what is said from <paramref name="Seconds"/> into the video, or, as a
/// <paramref name="Chapter"/>, the name of the chapter that starts there.</summary>
internal readonly record struct TranscriptLine(int Seconds, string Text, bool Chapter = false);

/// <summary>A video's transcript, as the page read it from YouTube's own transcript panel (Ctrl+S): the video (its
/// id, title and channel), the language YouTube chose, and the lines. The host writes it as a text file into the
/// Downloads folder: only a text file, only there, under a name made from the title, whatever the page sends.</summary>
internal sealed record PageTranscript(string Id, string Title, string Channel, string Language, IReadOnlyList<TranscriptLine> Lines)
{
    // The title's share of the file's name: with the id and the folder's path, well inside Windows' 260.
    private const int TitleLimit = 120;

    /// <summary>Reads the page's <c>transcript</c>; null unless it has a video's id and at least one line. A line that
    /// is neither text nor a chapter is left out.</summary>
    public static PageTranscript? From(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || Text(e, "id") is not { } id || !YouTubeUrl.IsVideoId(id)
            || !e.TryGetProperty("lines", out var array) || array.ValueKind != JsonValueKind.Array)
            return null;
        var lines = new List<TranscriptLine>(array.GetArrayLength());
        foreach (var line in array.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object
                || !line.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.Number
                || !t.TryGetInt32(out var seconds) || seconds < 0)
                continue;
            if (Text(line, "chapter") is { Length: > 0 } chapter)
                lines.Add(new TranscriptLine(seconds, chapter, Chapter: true));
            else if (Text(line, "text") is { Length: > 0 } text)
                lines.Add(new TranscriptLine(seconds, text));
        }
        return lines.Count == 0
            ? null
            : new PageTranscript(id, Text(e, "title") ?? "", Text(e, "channel") ?? "", Text(e, "language") ?? "", lines);
    }

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? OneLine(value.GetString()!) : null;

    /// <summary>The file's text: the title, the channel, the video's link and the transcript's language, then a line
    /// for each of the transcript's, its time first, as YouTube lists them. A chapter's name stands on its own line,
    /// set off by an empty one.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var heading in (string[])[Title, Channel, $"https://youtu.be/{Id}", Language])
            if (heading.Length > 0)
                text.Append(heading).Append('\n');
        var first = true;
        foreach (var line in Lines)
        {
            // An empty line before the transcript, and before each chapter.
            if (first || line.Chapter)
                text.Append('\n');
            first = false;
            if (!line.Chapter)
                text.Append(Clock(line.Seconds)).Append(' ');
            text.Append(line.Text).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The file's name: the title without what a file name may not hold, and the video's id, which keeps two
    /// videos of one title apart.</summary>
    public string FileName()
    {
        var name = new StringBuilder(Title.Length);
        foreach (var c in Title)
            name.Append(c < ' ' || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' ? ' ' : c);
        var title = OneLine(name.ToString());
        if (title.Length > TitleLimit)
            title = title[..(char.IsHighSurrogate(title[TitleLimit - 1]) ? TitleLimit - 1 : TitleLimit)].TrimEnd();
        // Windows drops a name's trailing dots; a name of nothing else is no name.
        title = title.TrimEnd('.', ' ');
        return $"{(title.Length > 0 ? title : "Transcript")} [{Id}].txt";
    }

    /// <summary>Writes the file into <paramref name="folder"/>, over one of the same name (the same video's), and
    /// gives its name. Never throws: null, with the reason in the log, when it could not be written.</summary>
    public string? TrySave(string folder)
    {
        var file = FileName();
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, file), ToText().ReplaceLineEndings());
            return file;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            PlayerLog.Write($"transcript: {file} was not written to {folder}: {ex.Message}");
            return null;
        }
    }

    /// <summary>A time into the video as YouTube writes it: 0:07, 12:34, 1:02:03.</summary>
    internal static string Clock(int seconds)
    {
        var (h, m, s) = (seconds / 3600, seconds / 60 % 60, seconds % 60);
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    // Line breaks and runs of white space as one space each, and none at the ends.
    private static string OneLine(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
