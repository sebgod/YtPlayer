using SdlVulkan.Renderer.WebView;

namespace YtPlayer;

/// <summary>What the host tells the page and asks of it, without ever taking the player down. A web view takes a
/// message, a script or a protocol call only once its view is up, and throws until then (WebView2: "not ready
/// yet"), and may refuse one later (its browser gone). These calls come from the strip's buttons and keys, where a
/// throw ends the player: a press on Home in a window whose web view never came up did (0.1.24, after an update),
/// and Back, play, next, compact, fullscreen and the media keys could have. So nothing is sent until the page has
/// been heard from, which every document does as it starts (<c>{tips}</c>), and a call the view still refuses is
/// logged, once for each kind, and dropped.</summary>
internal sealed class PageLink(INativeWebView web, Action<string> log)
{
    private readonly HashSet<string> _refused = [];

    /// <summary>The page has been heard from, so its view is up.</summary>
    public bool Up { get; private set; }

    /// <summary>A message came from the page.</summary>
    public void Heard() => Up = true;

    /// <summary>Posts <paramref name="json"/> to the page; false, and nothing sent, while there is none to take it.</summary>
    public bool Post(string json) => Try("a message", () => web.PostMessage(json));

    /// <summary>Runs <paramref name="script"/> in the page, its result not waited for.</summary>
    public bool Run(string script) => Try("a script", () => Watch("a script", web.ExecuteScriptAsync(script)));

    /// <summary>Calls a DevTools protocol method, its result not waited for (a click the page trusts, the user's
    /// gesture for fullscreen).</summary>
    public bool Protocol(string method, string parametersAsJson)
        => Try(method, () => Watch(method, web.CallDevToolsProtocolMethodAsync(method, parametersAsJson)));

    private bool Try(string what, Action call)
    {
        if (!Up)
            return false;
        try
        {
            call();
            return true;
        }
        // Whatever the web view throws: its failures are HRESULTs, which become exceptions of several kinds.
        catch (Exception ex)
        {
            Refused(what, ex);
            return false;
        }
    }

    // A call that fails later, on its task, is logged like one that throws at once.
    private void Watch(string what, Task call)
        => call.ContinueWith(t => Refused(what, t.Exception!.GetBaseException()),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    private void Refused(string what, Exception ex)
    {
        bool first;
        lock (_refused)
            first = _refused.Add(what);
        if (first)
            log($"the page did not take {what}: {ex.Message}");
    }
}
