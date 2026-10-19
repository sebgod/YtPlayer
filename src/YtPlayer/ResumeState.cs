using System.Text.Json;
using System.Text.Json.Serialization;

namespace YtPlayer;

/// <summary>What the player was doing when it updated itself, beyond what its settings keep (the window's place and
/// sizes, compact, stay on top) and the link it hands over (the video, at its time): for the new version to take up.
/// The old player writes it as it hands over (<see cref="SelfUpdate"/>); the new one, started to finish the update,
/// takes it, which deletes it.</summary>
internal sealed record ResumeState
{
    /// <summary>The video was paused: the new player pauses it once it starts.</summary>
    public bool Paused { get; init; }
    /// <summary>The window was folded to its strip.</summary>
    public bool Collapsed { get; init; }

    private static string FilePath => Path.Combine(PlayerSettings.DataFolder, "resume.json");

    /// <summary>Writes it for the next start. Never throws: losing it is a video that plays and a window unfolded.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PlayerSettings.DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, ResumeStateJson.Default.ResumeState));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PlayerLog.Write($"update: the state to resume was not saved: {ex.Message}");
        }
    }

    /// <summary>Reads it and deletes it, so it is taken up once; null when there is none.</summary>
    public static ResumeState? Take()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;
            var text = File.ReadAllText(FilePath);
            File.Delete(FilePath);
            return JsonSerializer.Deserialize(text, ResumeStateJson.Default.ResumeState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}

[JsonSerializable(typeof(ResumeState))]
internal sealed partial class ResumeStateJson : JsonSerializerContext;
