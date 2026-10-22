using System.ComponentModel;
using System.Runtime.InteropServices;
using Pulau.Core.Layout;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Pulau.Interop;

/// <summary>A monitor's work area (physical pixels, excludes the taskbar) and its DPI scale (1.0 = 100%).</summary>
internal readonly record struct MonitorPlacement(PixelRect WorkArea, double DpiScale);

internal static class Monitors
{
    /// <summary>The primary monitor. Choosing another monitor arrives with settings (Phase 5).</summary>
    public static MonitorPlacement Primary()
    {
        var monitor = PInvoke.MonitorFromPoint(new System.Drawing.Point(0, 0), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        return Describe(monitor);
    }

    private static MonitorPlacement Describe(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetMonitorInfo failed.");
        }

        // The process is Per-Monitor V2 DPI aware, so this is the monitor's real effective DPI.
        var dpi = PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded
            ? dpiX
            : 96u;

        var work = info.rcWork;
        return new MonitorPlacement(
            new PixelRect(work.left, work.top, work.right - work.left, work.bottom - work.top),
            dpi / 96.0);
    }
}
