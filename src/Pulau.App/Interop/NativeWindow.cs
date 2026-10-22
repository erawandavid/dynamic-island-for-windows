using Pulau.Core.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Pulau.Interop;

/// <summary>Win32 window operations the island needs that WPF doesn't expose.</summary>
internal static class NativeWindow
{
    /// <summary>
    /// Adds extended window styles. GWL_EXSTYLE is 32-bit on every architecture, so the
    /// non-"Ptr" Get/SetWindowLong pair is correct here.
    /// </summary>
    public static void AddExtendedStyles(HWND hwnd, WINDOW_EX_STYLE styles)
    {
        var current = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)(current | styles));
    }

    /// <summary>Moves and sizes the window (physical pixels) and puts it on top of the topmost band.</summary>
    public static void SetBoundsTopmost(HWND hwnd, PixelRect bounds) =>
        PInvoke.SetWindowPos(
            hwnd,
            HWND.HWND_TOPMOST,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);

    /// <summary>Puts the window back on top of the topmost band without moving, sizing or activating it.</summary>
    public static void ReassertTopmost(HWND hwnd) =>
        PInvoke.SetWindowPos(
            hwnd,
            HWND.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
}
