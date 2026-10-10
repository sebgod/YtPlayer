using System.Text.Json;
using System.Text.Json.Serialization;

namespace YtPlayer;

/// <summary>What the player remembers between runs: where the window was, its two sizes, and its modes.
/// Sizes are in window coordinates; unset, they come from <see cref="WindowFit"/>'s defaults, scaled
/// for the display.</summary>
internal sealed record PlayerSettings
{
    public int? X { get; init; }
    public int? Y { get; init; }
    /// <summary>The size while a compact video shows.</summary>
    public int? PipWidth { get; init; }
    public int? PipHeight { get; init; }
    /// <summary>The size for browsing (anything that is not a compact video).</summary>
    public int? BrowseWidth { get; init; }
    public int? BrowseHeight { get; init; }
    public bool OnTop { get; init; } = true;
    public bool Compact { get; init; } = true;

    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "YtPlayer");

    /// <summary>The web view's profile (cookies, so a YouTube sign-in survives a restart).</summary>
    public static string WebProfileFolder => Path.Combine(DataFolder, "WebView");

    private static string SettingsPath => Path.Combine(DataFolder, "settings.json");

    public static PlayerSettings Load()
    {
        try
        {
            using var stream = File.OpenRead(SettingsPath);
            return JsonSerializer.Deserialize(stream, PlayerSettingsJson.Default.PlayerSettings) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, PlayerSettingsJson.Default.PlayerSettings));
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the window position is not worth failing the exit over.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PlayerSettings))]
internal sealed partial class PlayerSettingsJson : JsonSerializerContext;
