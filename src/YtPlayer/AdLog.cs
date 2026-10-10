using System.Globalization;

namespace YtPlayer;

/// <summary>
/// Follows an ad from start to end for <see cref="PlayerLog"/>: when it started, each Skip the page
/// offered and the player clicked, and how it ended. An end soon after a click was a skip; one with no
/// click, or long after it, played out. Each method returns the line to log.
/// </summary>
internal sealed class AdLog
{
    private DateTime? _start;
    private DateTime? _lastClick;
    private int _clicks;

    public string Started(DateTime now)
    {
        // A full page load mid-ad never reports its end; that one is closed off here.
        var unfinished = _start is not null ? " (the previous ad never reported an end)" : "";
        (_start, _lastClick, _clicks) = (now, null, 0);
        return "ad started" + unfinished;
    }

    /// <param name="canClick">Whether the player can click it (a trusted click, WebView2 only).</param>
    public string SkipOffered(DateTime now, double x, double y, bool canClick)
    {
        var after = _start is { } start ? $" after {Seconds(now - start)}" : "";
        if (!canClick)
            return $"ad: Skip offered{after}; no trusted click on this platform, so it waits for the viewer";
        _clicks++;
        _lastClick = now;
        var again = _clicks > 1 ? $" (click {_clicks})" : "";
        return string.Create(CultureInfo.InvariantCulture, $"ad: Skip offered{after}, clicked at ({x:0}, {y:0}){again}");
    }

    /// <returns>The line, or null when no start was seen (the page reports no ad as it loads).</returns>
    public string? Ended(DateTime now)
    {
        if (_start is not { } start)
            return null;
        var how = _lastClick is { } click
            ? $"{Seconds(now - click)} after the last of {_clicks} Skip click{(_clicks == 1 ? "" : "s")}"
            : "played out, no Skip clicked";
        (_start, _lastClick, _clicks) = (null, null, 0);
        return $"ad ended after {Seconds(now - start)}: {how}";
    }

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.0 s", CultureInfo.InvariantCulture);
}
