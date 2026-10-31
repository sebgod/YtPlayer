using Microsoft.Extensions.Logging;

namespace YtPlayer;

/// <summary>
/// SdlVulkan.Renderer's log (<c>SdlVulkanLog.Logger</c>) with what is worth keeping copied into the player's log:
/// every warning and worse (a GPU frame over its budget, event 208; a late or stuck fence; a device lost or given up
/// on) and the GPU the renderer chose (event 501). Its stderr lines, which a windowed app shows no one, go on as
/// before. So after a hang the log says whether a frame of the player's ran long first, which the system's own GPU
/// timeout dumps (LiveKernelEvent 141) cannot.
/// </summary>
internal sealed class RendererLog(ILogger inner, Action<string> write) : ILogger
{
    /// <summary>The renderer's choice of GPU: its name, type and driver version.</summary>
    private const int DeviceSelected = 501;

    /// <summary>Whether a renderer line goes into the player's log.</summary>
    public static bool Keeps(LogLevel level, EventId eventId)
        => level is >= LogLevel.Warning and not LogLevel.None || eventId.Id == DeviceSelected;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        inner.Log(logLevel, eventId, state, exception, formatter);
        if (Keeps(logLevel, eventId))
            write($"renderer: {formatter(state, exception)}");
    }
}
