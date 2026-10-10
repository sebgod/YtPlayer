using System.Text.Json;

namespace YtPlayer;

/// <summary>A message from <c>Scripts/yt-tweaks.js</c> to the host. Each carries any of: <c>ready</c> (the
/// page wants the compact state), <c>showing</c> (whether compact is on screen: a watch page, compact on,
/// not searching; the window takes its PiP size while it is), <c>paste</c> (text pasted outside a text
/// field, which may be a link to open), <c>copy</c> (a link for the clipboard, from Ctrl+C or Ctrl+Shift+C),
/// <c>skipAt</c> (where an ad's Skip button is, in the page's CSS pixels, for the host to click),
/// <c>playing</c> (whether the player's video plays, for the strip's play/pause button).</summary>
internal readonly record struct PageMessage(
    bool Ready, bool? Showing, string? Paste, string? Copy = null, (double X, double Y)? SkipAt = null,
    bool? Playing = null)
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
                    : null);
        }
        catch (JsonException)
        {
            return default; // not ours
        }
    }
}
