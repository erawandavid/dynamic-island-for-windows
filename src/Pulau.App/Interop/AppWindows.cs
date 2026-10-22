using System.Diagnostics;
using System.Runtime.InteropServices;
using Pulau.Core.Apps;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Pulau.Interop;

/// <summary>Finds the main window of the app behind a media session and brings it to the front.</summary>
internal static class AppWindows
{
    private static readonly ILogger Logger = Log.ForContext(typeof(AppWindows));

    /// <summary>
    /// Brings the app's most recently used window to the front, restoring it if it is minimized.
    /// Call it while handling the user's click: Windows only lets a background app change the
    /// foreground window in response to the user's own input.
    /// </summary>
    /// <param name="appId">The media session's app id (AppUserModelID), e.g. "Spotify.exe".</param>
    /// <returns>False when no window was found and the app couldn't be opened another way.</returns>
    public static bool TryActivate(string appId)
    {
        if (FindWindow(appId) is { } window)
        {
            Activate(window);
            return true;
        }

        // A packaged (Store) app without a window, e.g. closed to the tray: let the shell open it.
        if (appId.Contains('!', StringComparison.Ordinal))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{appId}") { UseShellExecute = true });
            return true;
        }

        return false;
    }

    private static HWND? FindWindow(string appId)
    {
        var ownProcessId = (uint)Environment.ProcessId;
        var identities = new Dictionary<uint, ProcessIdentity>();
        HWND? visible = null;
        HWND? hidden = null;

        // EnumWindows goes from the front-most window backwards, so the first match is the most recently used.
        PInvoke.EnumWindows(
            (hwnd, _) =>
            {
                try
                {
                    if (!IsMainWindow(hwnd, out var isVisible))
                    {
                        return true;
                    }

                    PInvoke.GetWindowThreadProcessId(hwnd, out var processId);
                    if (processId == ownProcessId)
                    {
                        return true;
                    }

                    if (!identities.TryGetValue(processId, out var identity))
                    {
                        identity = Describe(processId);
                        identities[processId] = identity;
                    }

                    if (!AppIdMatcher.Matches(appId, identity))
                    {
                        return true;
                    }

                    if (isVisible)
                    {
                        visible = hwnd;
                        return false;
                    }

                    hidden ??= hwnd;
                    return true;
                }
                catch (Exception ex)
                {
                    // Never let an exception escape into native code.
                    Logger.Debug(ex, "Skipping a window while looking for {App}", appId);
                    return true;
                }
            },
            default);

        return visible ?? hidden;
    }

    /// <summary>
    /// A top-level app window: not owned by another window, not a tool window, with a title.
    /// Hidden windows only count if they have a caption (e.g. an app minimized to the tray).
    /// </summary>
    private static bool IsMainWindow(HWND hwnd, out bool isVisible)
    {
        isVisible = PInvoke.IsWindowVisible(hwnd);
        if (!PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull)
        {
            return false;
        }

        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((exStyle & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0 || PInvoke.GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        if (isVisible)
        {
            return true;
        }

        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return (style & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION;
    }

    private static ProcessIdentity Describe(uint processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process.IsInvalid)
        {
            return default;
        }

        return new ProcessIdentity(ImagePath(process), AppUserModelId(process), PackageFamilyName(process));
    }

    private static string? ImagePath(SafeHandle process)
    {
        Span<char> buffer = stackalloc char[1024];
        var length = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length)
            ? Text(buffer, length)
            : null;
    }

    /// <summary>Only packaged apps have one; desktop apps return null.</summary>
    private static string? AppUserModelId(SafeHandle process)
    {
        Span<char> buffer = stackalloc char[256];
        var length = (uint)buffer.Length;
        return PInvoke.GetApplicationUserModelId(process, ref length, buffer) == WIN32_ERROR.ERROR_SUCCESS
            ? Text(buffer, length)
            : null;
    }

    /// <summary>Only packaged apps have one; desktop apps return null.</summary>
    private static string? PackageFamilyName(SafeHandle process)
    {
        Span<char> buffer = stackalloc char[256];
        var length = (uint)buffer.Length;
        return PInvoke.GetPackageFamilyName(process, ref length, buffer) == WIN32_ERROR.ERROR_SUCCESS
            ? Text(buffer, length)
            : null;
    }

    private static string Text(ReadOnlySpan<char> buffer, uint length)
    {
        var text = buffer[..(int)Math.Min(length, (uint)buffer.Length)];
        var end = text.IndexOf('\0');
        return new string(end >= 0 ? text[..end] : text);
    }

    private static void Activate(HWND window)
    {
        if (PInvoke.IsIconic(window))
        {
            PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_RESTORE);
        }
        else if (!PInvoke.IsWindowVisible(window))
        {
            PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_SHOW);
        }

        if (!PInvoke.SetForegroundWindow(window))
        {
            Logger.Warning("Windows did not let Pulau bring the window to the front");
        }
    }
}
