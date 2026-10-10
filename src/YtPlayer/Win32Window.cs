using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static SDL3.SDL;

namespace YtPlayer;

/// <summary>Win32 adjustments SDL has no flag for.</summary>
[SupportedOSPlatform("windows")]
internal static partial class Win32Window
{
    private const int GwlStyle = -16;
    private const long WsMaximizeBox = 0x00010000;

    /// <summary>Takes the maximize box off the window's style. The strip is a caption to Windows (SDL's
    /// hit test answers HTCAPTION there), so a double-click on it maximized the window, and the drag
    /// that click began then stayed stuck to the cursor with no button held. A picture-in-picture window
    /// has no use for maximizing anyway; without the box, Windows ignores the double-click, and
    /// snapping to the top edge or Win+Up leaves the size alone too.</summary>
    public static void RemoveMaximize(nint sdlWindow)
    {
        var hwnd = Handle(sdlWindow);
        if (hwnd == nint.Zero)
            return;
        var style = GetWindowLongPtrW(hwnd, GwlStyle);
        SetWindowLongPtrW(hwnd, GwlStyle, (nint)((long)style & ~WsMaximizeBox));
    }

    /// <summary>The SDL window's HWND.</summary>
    public static nint Handle(nint sdlWindow)
        => GetPointerProperty(GetWindowProperties(sdlWindow), Props.WindowWin32HWNDPointer, nint.Zero);

    /// <summary>Whether <paramref name="hwnd"/> is the foreground window, the one keys go to. SDL's own
    /// input-focus flag will not do: a click into the page moves the keyboard focus to the web view's
    /// child window, and SDL counts that as losing focus while the window is still the active one.</summary>
    public static bool IsForeground(nint hwnd) => hwnd != nint.Zero && GetForegroundWindow() == hwnd;

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll")]
    private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}
