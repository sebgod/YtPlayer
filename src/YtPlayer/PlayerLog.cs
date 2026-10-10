namespace YtPlayer;

/// <summary>The player's log, <c>ytplayer.log</c> beside its settings: what it did on its own (an ad's Skip
/// pressed, a click that failed), to check after the fact, as a windowed app has no console to see. Kept
/// small: past 256 KB it starts over, the previous one kept as <c>ytplayer.log.1</c>.</summary>
internal static class PlayerLog
{
    private const long MaxBytes = 256 * 1024;
    private static readonly Lock Gate = new();

    public static string FilePath => Path.Combine(PlayerSettings.DataFolder, "ytplayer.log");

    /// <summary>Appends <paramref name="line"/> with the local time. Never throws: a log is not worth
    /// failing the player over.</summary>
    public static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(PlayerSettings.DataFolder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere left to report it.
        }
    }
}
