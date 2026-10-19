using System.IO.Compression;
using Shouldly;

namespace YtPlayer.Tests;

public sealed class SelfUpdateTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ytplayer-selfupdate-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string Marker = "0.1.14 win-arm64\nLICENSE\nSDL3.dll\nVERSION\nytplayer.exe\n";

    [Fact]
    public void TheMarkerIsTheVersionTheRuntimeAndTheFiles()
    {
        var build = SelfUpdate.ParseMarker(Marker).ShouldNotBeNull();
        build.Version.ShouldBe("0.1.14");
        build.Rid.ShouldBe("win-arm64");
        build.Files.ShouldBe(["LICENSE", "SDL3.dll", "VERSION", "ytplayer.exe"]);
    }

    [Fact]
    public void AMarkerWrittenOnWindowsReadsTheSame()
        => SelfUpdate.ParseMarker("﻿" + Marker.Replace("\n", "\r\n")).ShouldNotBeNull().Files.Count.ShouldBe(4);

    [Theory]
    [InlineData("")]
    [InlineData("0.1.14")] // no runtime
    [InlineData("latest win-arm64")]
    public void AnythingElseIsNoMarker(string text) => SelfUpdate.ParseMarker(text).ShouldBeNull();

    [Fact]
    public void AMarkerNamesOnlyFilesOfItsFolder()
        => SelfUpdate.ParseMarker("0.1.14 win-x64\n..\\settings.json\nsub/file.dll\nytplayer.exe").ShouldNotBeNull()
            .Files.ShouldBe(["ytplayer.exe"]);

    [Fact]
    public void OnlyAReleaseBuildOfThisVersionUpdatesItself()
    {
        var install = Folder("app", Marker);
        SelfUpdate.Installed(install, "0.1.14").ShouldNotBeNull();
        SelfUpdate.Installed(install, "0.1.13").ShouldBeNull(); // a marker left beside another build
        SelfUpdate.Installed(Folder("dev"), "0.1.14").ShouldBeNull(); // no marker: a build from source
        SelfUpdate.Installed(Folder(Path.Combine("app", "update"), Marker), "0.1.14").ShouldBeNull(); // the staged copy
    }

    [Fact]
    public void TheZipIsTheOneForThisRuntime()
    {
        var release = new Release("0.1.14", "https://github.com/sebgod/YtPlayer/releases/tag/v0.1.14")
        {
            Assets =
            [
                new ReleaseAsset("ytplayer-0.1.14-win-x64.zip", "x64", "aa"),
                new ReleaseAsset("ytplayer-0.1.14-win-arm64-symbols.zip", "symbols", "bb"),
                new ReleaseAsset("ytplayer-0.1.14-win-arm64.zip", "arm64", "cc"),
            ],
        };
        SelfUpdate.AssetFor(release, SelfUpdate.ParseMarker(Marker)!)!.Url.ShouldBe("arm64");
    }

    [Fact]
    public void AStepIsItsFlagThePidTheVersionAndTheLink()
    {
        var step = SelfUpdate.ParseStep(["--update-from", "4242", "0.1.14", "https://youtu.be/dQw4w9WgXcQ?t=83"])
            .ShouldNotBeNull();
        step.Flag.ShouldBe("--update-from");
        step.Pid.ShouldBe(4242);
        step.Version.ShouldBe("0.1.14");
        step.Rest.ShouldBe(["https://youtu.be/dQw4w9WgXcQ?t=83"]);
        SelfUpdate.ParseStep(["--update-done", "4242", "0.1.14"]).ShouldNotBeNull().Rest.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")] // an ordinary start
    [InlineData("--update-from", "x", "0.1.14")]
    [InlineData("--update-from", "4242", "latest")]
    [InlineData("--update-from", "4242")]
    [InlineData("--update-now", "4242", "0.1.14")]
    public void AnythingElseIsNoStep(params string[] args) => SelfUpdate.ParseStep(args).ShouldBeNull();

    [Fact]
    public void AStartThatIsNoStepGoesOnAsThePlayer()
    {
        string[] args = ["some", "search"];
        SelfUpdate.Continue(ref args).ShouldBeNull();
        args.ShouldBe(["some", "search"]);
    }

    [Fact]
    public void AReleaseZipUnpacksIntoTheStagingFolder()
    {
        var staging = Folder("staging");
        var zip = Zip(("ytplayer.exe", "new exe"), ("SDL3.dll", "sdl"), ("VERSION", Marker));
        SelfUpdate.Unpack(zip, staging, "0.1.14", "win-arm64");
        File.ReadAllText(Path.Combine(staging, "ytplayer.exe")).ShouldBe("new exe");
    }

    [Fact]
    public void AZipOfAnotherVersionOrRuntimeIsRefused()
    {
        var zip = Zip(("ytplayer.exe", "new exe"), ("VERSION", Marker));
        Should.Throw<InvalidDataException>(() => SelfUpdate.Unpack(zip, Folder("a"), "0.1.15", "win-arm64"));
        Should.Throw<InvalidDataException>(() => SelfUpdate.Unpack(zip, Folder("b"), "0.1.14", "win-x64"));
    }

    [Fact]
    public void AZipWithoutTheExeOrTheMarkerIsRefused()
    {
        Should.Throw<InvalidDataException>(() =>
            SelfUpdate.Unpack(Zip(("VERSION", Marker)), Folder("a"), "0.1.14", "win-arm64"));
        Should.Throw<InvalidDataException>(() =>
            SelfUpdate.Unpack(Zip(("ytplayer.exe", "x")), Folder("b"), "0.1.14", "win-arm64"));
    }

    [Fact]
    public void AZipThatReachesOutOfItsFolderIsRefused()
    {
        var staging = Folder("staging");
        Should.Throw<InvalidDataException>(() =>
            SelfUpdate.Unpack(Zip(("../settings.json", "{}"), ("ytplayer.exe", "x"), ("VERSION", Marker)), staging,
                "0.1.14", "win-arm64"));
        File.Exists(Path.Combine(_root, "settings.json")).ShouldBeFalse();
    }

    [Fact]
    public void TheSwapReplacesTheReleaseAndLeavesEverythingElse()
    {
        var install = Folder("app", "0.1.13 win-arm64\nLICENSE\nold-dependency.dll\nSDL3.dll\nVERSION\nytplayer.exe");
        File.WriteAllText(Path.Combine(install, "ytplayer.exe"), "old exe");
        File.WriteAllText(Path.Combine(install, "old-dependency.dll"), "old");
        File.WriteAllText(Path.Combine(install, "SDL3.dll"), "old sdl");
        File.WriteAllText(Path.Combine(install, "notes.txt"), "the user's"); // no release put it there
        var staged = Folder(Path.Combine("app", "update"), Marker);
        File.WriteAllText(Path.Combine(staged, "ytplayer.exe"), "new exe");
        File.WriteAllText(Path.Combine(staged, "SDL3.dll"), "new sdl");
        File.WriteAllText(Path.Combine(staged, "LICENSE"), "license");

        SelfUpdate.Swap(staged, install, SelfUpdate.ParseMarker(File.ReadAllText(Path.Combine(install, "VERSION")))!)
            .ShouldBeEmpty();

        File.ReadAllText(Path.Combine(install, "ytplayer.exe")).ShouldBe("new exe");
        File.ReadAllText(Path.Combine(install, "SDL3.dll")).ShouldBe("new sdl");
        File.ReadAllText(Path.Combine(install, "VERSION")).ShouldBe(Marker);
        File.Exists(Path.Combine(install, "old-dependency.dll")).ShouldBeFalse();
        File.ReadAllText(Path.Combine(install, "notes.txt")).ShouldBe("the user's");
        Directory.Exists(staged).ShouldBeTrue(); // the installed exe clears it, once the staged one has exited
    }

    [Fact]
    public void TheChecksumIsSha256InLowercaseHex()
    {
        var file = Path.Combine(_root, "abc.txt");
        File.WriteAllText(file, "abc");
        SelfUpdate.Sha256Of(file).ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    private string Folder(string name, string? marker = null)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        if (marker is not null)
            File.WriteAllText(Path.Combine(path, "VERSION"), marker);
        return path;
    }

    private string Zip(params (string Name, string Text)[] entries)
    {
        var path = Path.Combine(_root, $"release-{Guid.NewGuid():N}.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(text);
        }
        return path;
    }
}
