using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace YtPlayer;

/// <summary>A release build, as its <c>VERSION</c> marker says: the version, the runtime it was built for
/// (<c>win-arm64</c>), and the files the release put in its folder.</summary>
internal sealed record ReleaseBuild(string Version, string Rid, IReadOnlyList<string> Files);

/// <summary>What staging an update came to: the staged exe and its version, or why there is none.</summary>
internal sealed record StagedUpdate(string? Exe, string? Version, string? Error);

/// <summary>
/// A release build updates itself, in three processes, none of which shows a window until the last:
/// <list type="number">
/// <item>The player downloads the newer release's zip for its runtime into <c>update\</c> beside its exe, checks it
/// against the SHA-256 GitHub lists for it, and unpacks it there. It starts <c>update\ytplayer.exe --update-from
/// &lt;its pid&gt; &lt;version&gt; [link]</c>, hides its window and quits as usual (its settings saved).</item>
/// <item>The staged copy (<see cref="Continue"/>) waits for that pid to exit, copies its folder's files over the
/// install, deletes the files the old release had and the new one does not, starts the installed exe with
/// <c>--update-done &lt;its pid&gt; &lt;version&gt; [link]</c>, and exits.</item>
/// <item>The installed exe waits for the staged one to exit, deletes <c>update\</c>, and goes on as the player,
/// with the link (the video that was playing, at its time).</item>
/// </list>
/// Each step only moves the chain on: the staged copy acts only from <c>update\</c> under a release install, as
/// the version it was asked to be, and only starts the installed exe; the installed exe, given --update-done, starts
/// nothing; and nothing starts an update but the user's click. Only a release build (one with the marker, which
/// only the release workflow writes) updates itself, Windows only for now.
/// </summary>
internal static class SelfUpdate
{
    public const string MarkerName = "VERSION";
    public const string StagingName = "update";
    private const string ExeName = "ytplayer.exe";
    private const string FromFlag = "--update-from";
    private const string DoneFlag = "--update-done";
    // How long a step waits for the one before it to exit: the player saves its settings and closes its web view.
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(60);

    /// <summary>The folder the running exe is in.</summary>
    public static string AppFolder { get; } = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    /// <summary>The release build this is, if it is one that may update itself: its marker beside the exe, for
    /// this build's version, and not the staged copy. Null for any other build.</summary>
    public static ReleaseBuild? Installed()
        => OperatingSystem.IsWindows() ? Installed(AppFolder, UpdateCheck.CurrentVersion) : null;

    internal static ReleaseBuild? Installed(string folder, string version)
        => !IsStaging(folder) && ReadMarker(folder) is { } build && build.Version == version ? build : null;

    /// <summary>Reads a marker: <c>&lt;version&gt; &lt;rid&gt;</c>, then the release's files, a line each.</summary>
    internal static ReleaseBuild? ParseMarker(string text)
    {
        var lines = text.TrimStart('﻿').Split('\n',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return null;
        var head = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (head.Length != 2 || !IsVersion(head[0]))
            return null;
        // A file is a name in the folder, nothing below or above it.
        var files = lines.Skip(1).Where(IsPlainName).ToArray();
        return new ReleaseBuild(head[0], head[1], files);
    }

    private static ReleaseBuild? ReadMarker(string folder)
    {
        try
        {
            return ParseMarker(File.ReadAllText(Path.Combine(folder, MarkerName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The release's zip for <paramref name="build"/>'s runtime, as the release workflow names it.</summary>
    internal static ReleaseAsset? AssetFor(Release release, ReleaseBuild build)
        => release.Assets.FirstOrDefault(a => a.Name == $"ytplayer-{release.Version}-{build.Rid}.zip");

    /// <summary>Step 1, the download: the latest release, if it is newer, checked and unpacked into
    /// <c>update\</c>. Never throws; a failure is the result's <see cref="StagedUpdate.Error"/>, and logged.</summary>
    public static async Task<StagedUpdate> StageAsync(ReleaseBuild build, CancellationToken cancellationToken = default)
    {
        var staging = Path.Combine(AppFolder, StagingName);
        try
        {
            if (await UpdateCheck.LatestAsync(cancellationToken).ConfigureAwait(false) is not { } release)
                return Failed("GitHub gave no latest release");
            if (!UpdateCheck.IsNewer(build.Version, release.Version))
                return Failed($"{release.Version} is not newer than {build.Version}");
            if (AssetFor(release, build) is not { } asset)
                return Failed($"{release.Version} has no zip for {build.Rid}");
            if (asset.Sha256 is null)
                return Failed($"GitHub lists no SHA-256 for {asset.Name}");

            // What an earlier attempt left goes first; if it cannot (a staged copy still running), this one stops.
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);
            var zip = Path.Combine(staging, asset.Name);
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"YtPlayer/{UpdateCheck.CurrentVersion}");
                await using var download = await http.GetStreamAsync(asset.Url, cancellationToken).ConfigureAwait(false);
                await using var file = File.Create(zip);
                await download.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }
            if (Sha256Of(zip) != asset.Sha256)
                return Failed($"{asset.Name} is not the file GitHub lists (its SHA-256 differs)");
            Unpack(zip, staging, release.Version, build.Rid);
            File.Delete(zip);
            PlayerLog.Write($"update: {release.Version} downloaded and checked, into {staging}");
            return new StagedUpdate(Path.Combine(staging, ExeName), release.Version, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException
                                       or InvalidDataException or TaskCanceledException or OperationCanceledException)
        {
            return Failed(ex.Message);
        }

        static StagedUpdate Failed(string why)
        {
            PlayerLog.Write($"update: not updated: {why}");
            return new StagedUpdate(null, null, why);
        }
    }

    /// <summary>Unpacks a release zip into <paramref name="staging"/>, which it must be all of: plain files, the exe
    /// and a marker for <paramref name="version"/> and <paramref name="rid"/> among them.</summary>
    /// <exception cref="InvalidDataException">The zip is not that release.</exception>
    internal static void Unpack(string zip, string staging, string version, string rid)
    {
        using (var archive = ZipFile.OpenRead(zip))
        {
            foreach (var entry in archive.Entries)
            {
                if (!IsPlainName(entry.FullName))
                    throw new InvalidDataException($"the zip has {entry.FullName}, which is not a file of its own");
                entry.ExtractToFile(Path.Combine(staging, entry.FullName), overwrite: true);
            }
        }
        if (!File.Exists(Path.Combine(staging, ExeName)))
            throw new InvalidDataException($"the zip has no {ExeName}");
        if (ReadMarker(staging) is not { } marker || marker.Version != version || marker.Rid != rid)
            throw new InvalidDataException($"the zip's {MarkerName} is not {version} {rid}");
    }

    internal static string Sha256Of(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Starts the staged exe for step 2. False, and logged, if it did not start.</summary>
    public static bool StartStaged(StagedUpdate staged, string? link)
        => staged is { Exe: { } exe, Version: { } version }
           && Start(exe, FromFlag, version, link);

    /// <summary>At start, before anything else: a step of an update in progress (step 2 or 3), if the arguments
    /// say so. Returns the exit code for a process that was only the step (2), or null for one that goes on as the
    /// player (3, and any start that is no step), with <paramref name="args"/> what is left for it: the link.</summary>
    public static int? Continue(ref string[] args)
    {
        if (ParseStep(args) is not { } step)
            return null;
        args = step.Rest;
        return step.Flag == FromFlag ? SwapIn(step) : FinishUp(step);
    }

    internal sealed record Step(string Flag, int Pid, string Version, string[] Rest);

    internal static Step? ParseStep(string[] args)
        => args is [var flag, var pid, var version, .. var rest]
           && flag is FromFlag or DoneFlag
           && int.TryParse(pid, out var id) && id > 0
           && IsVersion(version)
            ? new Step(flag, id, version, rest)
            : null;

    // Step 2: the staged copy swaps itself in for the install it is in.
    private static int SwapIn(Step step)
    {
        var install = Path.GetDirectoryName(AppFolder);
        if (!IsStaging(AppFolder) || install is null || ReadMarker(install) is not { } old
            || ReadMarker(AppFolder) is not { } staged || staged.Version != step.Version
            || UpdateCheck.CurrentVersion != step.Version)
        {
            PlayerLog.Write($"update: refused to swap {AppFolder} in for {step.Version}: not a staged release of it");
            return 1;
        }
        if (!WaitForExit(step.Pid))
        {
            PlayerLog.Write($"update: the player ({step.Pid}) did not exit; {step.Version} is left in {AppFolder}");
            return 1;
        }
        foreach (var problem in Swap(AppFolder, install, old))
            PlayerLog.Write($"update: {problem}");
        return Start(Path.Combine(install, ExeName), DoneFlag, step.Version, step.Rest.FirstOrDefault()) ? 0 : 1;
    }

    // Step 3: the installed exe, once the staged copy has exited, clears it away and goes on as the player.
    private static int? FinishUp(Step step)
    {
        if (IsStaging(AppFolder))
        {
            PlayerLog.Write("update: refused to finish an update from the staging folder");
            return 1;
        }
        WaitForExit(step.Pid);
        var staging = Path.Combine(AppFolder, StagingName);
        for (var attempt = 0; Directory.Exists(staging) && attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }
        PlayerLog.Write(UpdateCheck.CurrentVersion == step.Version
            ? $"update: now {step.Version}"
            : $"update: did not take: this is {UpdateCheck.CurrentVersion}, not {step.Version}");
        return null;
    }

    /// <summary>Copies the staged release's files over the install, then deletes those the old release had and the
    /// new one does not (a dependency removed or renamed). Files the old release did not list are never touched.
    /// Returns what went wrong, if anything.</summary>
    internal static List<string> Swap(string staged, string install, ReleaseBuild old)
    {
        var problems = new List<string>();
        var names = Directory.GetFiles(staged).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (!Retry(() => File.Copy(Path.Combine(staged, name), Path.Combine(install, name), overwrite: true)))
                problems.Add($"could not copy {name} into {install}");
        }
        foreach (var name in old.Files.Where(f => !names.Contains(f)))
        {
            var path = Path.Combine(install, name);
            if (File.Exists(path) && !Retry(() => File.Delete(path)))
                problems.Add($"could not delete {name}, which {old.Version} had");
        }
        return problems;
    }

    private static bool Retry(Action action)
    {
        // A file the exited process held can take a moment to let go.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 20)
                    return false;
                Thread.Sleep(250);
            }
        }
    }

    private static bool WaitForExit(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.WaitForExit(ExitWait);
        }
        catch (ArgumentException)
        {
            return true; // gone already
        }
    }

    private static bool Start(string exe, string flag, string version, string? link)
    {
        try
        {
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            start.ArgumentList.Add(flag);
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(version);
            if (!string.IsNullOrWhiteSpace(link))
                start.ArgumentList.Add(link);
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            PlayerLog.Write($"update: could not start {exe}: {ex.Message}");
            return false;
        }
    }

    // 0.1.14, or 0.2.0-rc1: what a release's tag is, less its v.
    private static bool IsVersion(string text) => Version.TryParse(text.Split('-')[0], out _);

    private static bool IsStaging(string folder)
        => string.Equals(Path.GetFileName(folder), StagingName, StringComparison.OrdinalIgnoreCase);

    private static bool IsPlainName(string name)
        => name.Length > 0 && name == Path.GetFileName(name) && name is not ("." or "..")
           && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
}
