using System.Text.Json;

namespace YtPlayer;

/// <summary>A message from <c>Scripts/yt-tweaks.js</c> to the host. Each carries any of: <c>ready</c> (the
/// page wants the compact state), <c>watching</c> (whether it is a watch page, so compact is showing),
/// <c>paste</c> (text pasted outside a text field, which may be a link to open).</summary>
internal readonly record struct PageMessage(bool Ready, bool? Watching, string? Paste)
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
                Watching: root.TryGetProperty("watching", out var w) && w.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? w.GetBoolean()
                    : null,
                Paste: root.TryGetProperty("paste", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null);
        }
        catch (JsonException)
        {
            return default; // not ours
        }
    }
}
