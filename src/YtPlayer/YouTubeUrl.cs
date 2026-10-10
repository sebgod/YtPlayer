using System.Text.RegularExpressions;

namespace YtPlayer;

/// <summary>Turns what the user typed (a video URL, a youtu.be or Shorts link, a bare video id, or search
/// words) into the YouTube page the player opens.</summary>
internal static partial class YouTubeUrl
{
    public const string Home = "https://www.youtube.com/";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdPattern();

    public static string ToStartUrl(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
            return Home;

        if (TryGetVideoId(text, out var id, out var start))
            return WatchUrl(id, start);

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && IsYouTubeHost(uri.Host))
            return uri.AbsoluteUri;

        return $"{Home}results?search_query={Uri.EscapeDataString(text)}";
    }

    public static string WatchUrl(string id, string? start = null)
        => start is null ? $"{Home}watch?v={id}" : $"{Home}watch?v={id}&t={Uri.EscapeDataString(start)}";

    /// <summary>Finds the video id in a bare id, a <c>watch?v=</c>, <c>youtu.be/</c>, <c>/shorts/</c>,
    /// <c>/embed/</c> or <c>/live/</c> URL. <paramref name="start"/> carries a <c>t=</c> start time.</summary>
    public static bool TryGetVideoId(string text, out string id, out string? start)
    {
        id = "";
        start = null;
        if (VideoIdPattern().IsMatch(text))
        {
            id = text;
            return true;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !IsYouTubeHost(uri.Host))
            return false;

        var query = ParseQuery(uri.Query);
        query.TryGetValue("t", out start);

        string? candidate;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
            candidate = segments.FirstOrDefault();
        else if (segments is ["watch"])
            candidate = query.GetValueOrDefault("v");
        else if (segments is ["shorts" or "embed" or "live" or "v", var s, ..])
            candidate = s;
        else
            candidate = null;

        if (candidate is null || !VideoIdPattern().IsMatch(candidate))
        {
            start = null;
            return false;
        }
        id = candidate;
        return true;
    }

    private static bool IsYouTubeHost(string host)
        => host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
           || host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
           || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result.TryAdd(key, value);
        }
        return result;
    }
}
