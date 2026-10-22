using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Pulau.Interop;

/// <summary>
/// Raises a callback whenever another process's window becomes the foreground window
/// (<c>EVENT_SYSTEM_FOREGROUND</c>). Event-driven, so it costs nothing while nothing changes.
/// Must be created on a thread with a message loop (the UI thread); callbacks arrive on that thread.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ForegroundWatcher>();

    private readonly Action _onForegroundChanged;

    // Kept in a field: native code holds a pointer to this delegate for the lifetime of the hook.
    private readonly WINEVENTPROC _callback;
    private readonly UnhookWinEventSafeHandle _hook;

    public ForegroundWatcher(Action onForegroundChanged)
    {
        _onForegroundChanged = onForegroundChanged;
        _callback = OnWinEvent;
        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            null,
            _callback,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);

        if (_hook.IsInvalid)
        {
            Logger.Warning("SetWinEventHook failed; the island won't re-assert topmost on foreground changes");
        }
    }

    public void Dispose() => _hook.Dispose();

    private void OnWinEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint idEventThread, uint eventTime)
    {
        // Exceptions must not escape into native code.
        try
        {
            _onForegroundChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Foreground change handler failed");
        }
    }
}
