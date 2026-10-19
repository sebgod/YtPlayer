using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace YtPlayer;

/// <summary>A release on GitHub: its version (the tag without its <c>v</c>) and its page, and its downloads, as GitHub
/// lists them (none for one remembered in settings).</summary>
internal sealed record Release(string Version, string Url)
{
    public IReadOnlyList<ReleaseAsset> Assets { get; init; } = [];
}

/// <summary>A release's download: its file name, where to get it, and its SHA-256 as GitHub computed it on upload
/// (null when GitHub gave none).</summary>
internal sealed record ReleaseAsset(string Name, string Url, string? Sha256);

/// <summary>
/// Whether a newer YtPlayer is out: GitHub's latest release, against the version this build carries (the release
/// workflow's <c>-p:Version</c>, from the tag). A release build can then update itself (<see cref="SelfUpdate"/>);
/// any other is pointed at the release page.
/// </summary>
internal static class UpdateCheck
{
    private const string LatestApi = "https://api.github.com/repos/sebgod/YtPlayer/releases/latest";
    /// <summary>Where a release's page must be for the player to open it.</summary>
    public const string ReleasesPage = "https://github.com/sebgod/YtPlayer/releases/";
    /// <summary>Where a release's download must be for the player to fetch it.</summary>
    public const string ReleaseDownloads = "https://github.com/sebgod/YtPlayer/releases/download/";

    /// <summary>How long a check's answer holds: at start, an older one is asked again.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromDays(1);

    /// <summary>This build's version, without the commit the SDK appends (<c>0.1.12+3969354…</c>). A build not made
    /// from a tag has the SDK's 1.0.0, which no release is newer than.</summary>
    public static string CurrentVersion { get; } = Plain(
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0");

    /// <summary>Asks GitHub for the latest release; null when there is no answer to be had (offline, rate-limited,
    /// not ours). Never throws: an update check is not worth failing anything over.</summary>
    public static async Task<Release?> LatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // GitHub's API refuses a request without one.
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YtPlayer", CurrentVersion));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var json = await http.GetStringAsync(LatestApi, cancellationToken).ConfigureAwait(false);
            return ParseLatest(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            PlayerLog.Write($"update check: {ex.GetBaseException().Message}");
            return null;
        }
    }

    /// <summary>Reads GitHub's answer for the latest release; null when it is not one, or its page is not ours.</summary>
    internal static Release? ParseLatest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String
                   && root.TryGetProperty("html_url", out var url) && url.ValueKind == JsonValueKind.String
                   && Plain(tag.GetString()!) is { Length: > 0 } version
                   && url.GetString() is { } page && page.StartsWith(ReleasesPage, StringComparison.Ordinal)
                ? new Release(version, page) { Assets = AssetsOf(root) }
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The release's downloads that are ours; one without a name or an address of ours is left out.
    private static ReleaseAsset[] AssetsOf(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<ReleaseAsset>();
        foreach (var a in assets.EnumerateArray())
        {
            if (a.ValueKind == JsonValueKind.Object
                && a.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                && a.TryGetProperty("browser_download_url", out var url) && url.ValueKind == JsonValueKind.String
                && url.GetString() is { } address && address.StartsWith(ReleaseDownloads, StringComparison.Ordinal))
            {
                var digest = a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                const string sha256 = "sha256:";
                list.Add(new ReleaseAsset(name.GetString()!, address,
                    digest is not null && digest.StartsWith(sha256, StringComparison.OrdinalIgnoreCase)
                        ? digest[sha256.Length..].ToLowerInvariant()
                        : null));
            }
        }
        return [.. list];
    }

    /// <summary>Whether <paramref name="latest"/> is newer than <paramref name="current"/>; a version either cannot
    /// read as one is not.</summary>
    internal static bool IsNewer(string current, string latest)
        => Version.TryParse(Numbers(current), out var have) && Version.TryParse(Numbers(latest), out var offered)
           && offered > have;

    // A tag's or a build's version as people read it: no v in front, no commit after.
    private static string Plain(string version)
    {
        var v = version.Trim();
        if (v.StartsWith('v') || v.StartsWith('V'))
            v = v[1..];
        var plus = v.IndexOf('+');
        return plus < 0 ? v : v[..plus];
    }

    // The numbers alone, for Version: a pre-release's suffix (0.2.0-rc1) is left off.
    private static string Numbers(string version)
    {
        var v = Plain(version);
        var dash = v.IndexOf('-');
        return dash < 0 ? v : v[..dash];
    }
}
