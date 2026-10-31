using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace YtPlayer.Tests;

public class RendererLogTests
{
    [Fact]
    public void ASlowGpuFrameReachesThePlayersLog()
    {
        var lines = new List<string>();
        var log = new RendererLog(NullLogger.Instance, lines.Add);
        log.Log(LogLevel.Warning, new EventId(208), "GPU frame 12 took 2300ms on the GPU", null, (s, _) => s);
        lines.ShouldBe(["renderer: GPU frame 12 took 2300ms on the GPU"]);
    }

    [Fact]
    public void TheChosenGpuIsKeptButEveryFramesTimingIsNot()
    {
        RendererLog.Keeps(LogLevel.Information, new EventId(501)).ShouldBeTrue();
        RendererLog.Keeps(LogLevel.Information, new EventId(119)).ShouldBeFalse();
        RendererLog.Keeps(LogLevel.Critical, new EventId(202)).ShouldBeTrue();
        RendererLog.Keeps(LogLevel.None, new EventId(208)).ShouldBeFalse();
    }

    [Fact]
    public void TheRenderersOwnLoggerStillGetsEveryLine()
    {
        var inner = new Recording();
        var log = new RendererLog(inner, _ => { });
        log.Log(LogLevel.Information, new EventId(119), "frame timing", null, (s, _) => s);
        inner.Lines.ShouldBe(["frame timing"]);
    }

    private sealed class Recording : ILogger
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
