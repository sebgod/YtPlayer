using System.Text.Json;

namespace YtPlayer;

/// <summary>A message from <c>Scripts/yt-tweaks.js</c> to the host. Each carries any of: <c>ready</c> (the
/// page wants the compact state), <c>showing</c> (whether compact is on screen: a watch page, compact on,
/// not searching; the window takes its PiP size while it is), <c>paste</c> (text pasted outside a text
/// field, which may be a link to open), <c>copy</c> (a link for the clipboard, from Ctrl+C or Ctrl+Shift+C),
/// <c>skipAt</c> (where an ad's Skip button is, in the page's CSS pixels, for the host to click),
/// <c>playing</c> (whether the player's video plays, for the strip's play/pause button), <c>ad</c> (an ad
/// started or ended, for the log), <c>hide</c> (Ctrl+H: hide the window), <c>collapse</c> (Ctrl+Up folds the
/// window to its strip, Ctrl+Down unfolds it), <c>queueClipboard</c> (Ctrl+Q with no video under the pointer:
/// queue the one whose link is on the clipboard), <c>media</c> (the track YouTube gave the page's media session,
/// for the window's entry in the system's media controls, or null when it took it away: read as
/// <see cref="MediaGone"/>), <c>help</c> (F1: open or close the help panel, which the host fills in),
/// <c>openRelease</c> (the help panel's link to a newer release, which the host opens in the
/// default browser: the page names no address, so only the release the host found is ever opened).</summary>
internal readonly record struct PageMessage(
    bool Ready, bool? Showing, string? Paste, string? Copy = null, (double X, double Y)? SkipAt = null,
    bool? Playing = null, bool? Ad = null, bool Hide = false, bool? Collapse = null, bool QueueClipboard = false,
    PageTrack? Media = null, bool MediaGone = false, bool Help = false, bool OpenRelease = false)
{
    /// <summary>Reads <paramref name="json"/>; anything that is not one of ours reads as empty.</summary>
    public static PageMessage Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return default;
            return new PageMessage(
                Ready: root.TryGetProperty("ready", out _),
                Showing: root.TryGetProperty("showing", out var s) && s.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? s.GetBoolean()
                    : null,
                Paste: root.TryGetProperty("paste", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null,
                Copy: root.TryGetProperty("copy", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null,
                SkipAt: root.TryGetProperty("skipAt", out var at) && at.ValueKind == JsonValueKind.Object
                    && at.TryGetProperty("x", out var x) && x.ValueKind == JsonValueKind.Number
                    && at.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number
                        ? (x.GetDouble(), y.GetDouble())
                        : null,
                Playing: root.TryGetProperty("playing", out var pl) && pl.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? pl.GetBoolean()
                    : null,
                Ad: root.TryGetProperty("ad", out var ad) && ad.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? ad.GetBoolean()
                    : null,
                Hide: root.TryGetProperty("hide", out var h) && h.ValueKind == JsonValueKind.True,
                Collapse: root.TryGetProperty("collapse", out var fold) && fold.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? fold.GetBoolean()
                    : null,
                QueueClipboard: root.TryGetProperty("queueClipboard", out var q) && q.ValueKind == JsonValueKind.True,
                Media: root.TryGetProperty("media", out var m) ? PageTrack.From(m) : null,
                MediaGone: root.TryGetProperty("media", out var gone) && gone.ValueKind == JsonValueKind.Null,
                Help: root.TryGetProperty("help", out var help) && help.ValueKind == JsonValueKind.True,
                OpenRelease: root.TryGetProperty("openRelease", out var release) && release.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return default; // not ours
        }
    }
}

/// <summary>A track as YouTube describes it to the page's media session: its title, the channel (as the artist), a
/// picture of it (a URL), and whether YouTube offers the previous and next ones.</summary>
internal sealed record PageTrack(string Title, string Artist, string? Artwork, bool Previous, bool Next)
{
    public static PageTrack? From(JsonElement e)
        => e.ValueKind == JsonValueKind.Object
           && e.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
           && title.GetString() is { Length: > 0 } t
            ? new PageTrack(t,
                e.TryGetProperty("artist", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! : "",
                e.TryGetProperty("artwork", out var art) && art.ValueKind == JsonValueKind.String ? art.GetString() : null,
                e.TryGetProperty("previous", out var p) && p.ValueKind == JsonValueKind.True,
                e.TryGetProperty("next", out var n) && n.ValueKind == JsonValueKind.True)
            : null;
}
