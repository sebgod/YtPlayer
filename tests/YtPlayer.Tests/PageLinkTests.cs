using DIR.Lib;
using SdlVulkan.Renderer;
using SdlVulkan.Renderer.WebView;
using Shouldly;

namespace YtPlayer.Tests;

public class PageLinkTests
{
    [Fact]
    public void NothingIsSentUntilThePageHasBeenHeardFrom()
    {
        // A web view that is not up throws on each of these, and a strip button's press would end the player on it.
        var web = new FakeWebView { NotReady = true };
        var log = new List<string>();
        var page = new PageLink(web, log.Add);

        page.Post("""{"home":true}""").ShouldBeFalse();
        page.Run("history.back()").ShouldBeFalse();
        page.Protocol("Runtime.evaluate", "{}").ShouldBeFalse();

        web.Calls.ShouldBeEmpty();
        log.ShouldBeEmpty();
    }

    [Fact]
    public void OnceThePageIsHeardFromEverythingGoesToIt()
    {
        var web = new FakeWebView();
        var page = new PageLink(web, _ => { });
        page.Up.ShouldBeFalse();

        page.Heard();

        page.Up.ShouldBeTrue();
        page.Post("""{"home":true}""").ShouldBeTrue();
        page.Run("history.back()").ShouldBeTrue();
        page.Protocol("Runtime.evaluate", "{}").ShouldBeTrue();
        web.Calls.ShouldBe(["post {\"home\":true}", "run history.back()", "protocol Runtime.evaluate {}"]);
    }

    [Fact]
    public void WhatTheWebViewRefusesIsLoggedOnceAndDropped()
    {
        // Heard from, and then its view gone: the web view throws again.
        var web = new FakeWebView();
        var log = new List<string>();
        var page = new PageLink(web, log.Add);
        page.Heard();
        web.NotReady = true;

        page.Post("""{"next":true}""").ShouldBeFalse();
        page.Post("""{"next":true}""").ShouldBeFalse();
        page.Run("history.back()").ShouldBeFalse();
        page.Protocol("Input.dispatchMouseEvent", "{}").ShouldBeFalse();

        log.ShouldBe([
            "the page did not take a message: WebView2 is not ready yet.",
            "the page did not take a script: WebView2 is not ready yet.",
            "the page did not take Input.dispatchMouseEvent: WebView2 is not ready yet.",
        ]);
    }

    [Fact]
    public async Task ACallThatFailsLaterIsLoggedToo()
    {
        var web = new FakeWebView { Later = new TaskCompletionSource<string>() };
        var logged = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new PageLink(web, logged.SetResult);
        page.Heard();

        page.Protocol("Runtime.evaluate", "{}").ShouldBeTrue();
        web.Later.SetException(new InvalidOperationException("Runtime.evaluate failed (HRESULT 0x80004004)."));

        (await logged.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .ShouldBe("the page did not take Runtime.evaluate: Runtime.evaluate failed (HRESULT 0x80004004).");
    }

    // Records what it is asked; NotReady makes it throw as WebView2 does before its view is up.
#pragma warning disable CS0067 // the events are the interface's; nothing here raises them
    private sealed class FakeWebView : INativeWebView
    {
        public List<string> Calls { get; } = [];
        public bool NotReady { get; set; }
        public TaskCompletionSource<string>? Later { get; init; }

        private void Take(string call)
        {
            if (NotReady)
                throw new InvalidOperationException("WebView2 is not ready yet.");
            Calls.Add(call);
        }

        public void PostMessage(string json) => Take($"post {json}");

        public Task<string> ExecuteScriptAsync(string javaScript)
        {
            Take($"run {javaScript}");
            return Later?.Task ?? Task.FromResult("null");
        }

        public Task<string> CallDevToolsProtocolMethodAsync(string method, string parametersAsJson)
        {
            Take($"protocol {method} {parametersAsJson}");
            return Later?.Task ?? Task.FromResult("{}");
        }

        public void AttachToWindow(SdlVulkanWindow window) { }
        public void Navigate(string url) { }
        public void NavigateToString(string html) { }
        public void SetBounds(RectInt bounds) { }
        public void Focus() { }
        public void SetVisible(bool visible) { }
        public void SetMemoryUsageTarget(bool low) { }
        public Task<bool> TrySuspendAsync() => Task.FromResult(false);
        public void Resume() { }
        public Task AddScriptAtDocumentStartAsync(string javaScript) => Task.CompletedTask;
        public void Dispose() { }

        public event Action<string>? TitleChanged;
        public event Action<bool>? FullscreenChanged;
        public event Action<string>? NavigationCompleted;
        public event Action<string>? MessageReceived;
        public event Action<nint, uint, nint, nint>? WndProcOverride;
        public event Action<string>? Trace;
        public event Action<string, string>? ConsoleMessage;
        public event Action<string>? PageError;
    }
#pragma warning restore CS0067
}
