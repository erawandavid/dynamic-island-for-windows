using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Pulau.Core.Layout;
using Pulau.Interop;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Pulau.Views;

/// <summary>
/// The always-on-top, non-activating, click-through host window at the top center of the screen.
/// </summary>
public partial class IslandWindow : Window
{
    private static readonly ILogger Logger = Log.ForContext<IslandWindow>();

    // Broadcast to top-level windows when Explorer (and so the taskbar) starts or restarts.
    private static readonly uint TaskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");

    private HwndSource? _source;
    private ForegroundWatcher? _foregroundWatcher;
    private bool _repositionPending;

    public IslandWindow()
    {
        InitializeComponent();
        Width = IslandPlacement.WindowWidthDip;
        Height = IslandPlacement.WindowHeightDip;
        Pill.Margin = new Thickness(0, IslandPlacement.PillTopMarginDip, 0, 0);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                ScheduleReposition("shown");
            }
        };
    }

    private HWND Handle => new(_source?.Handle ?? IntPtr.Zero);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        _source.AddHook(WndProc);

        // Keep the island out of Alt+Tab and the taskbar, and never let it take focus.
        NativeWindow.AddExtendedStyles(Handle, WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE);

        _foregroundWatcher = new ForegroundWatcher(OnForegroundChanged);
        Reposition("initial placement");
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ScheduleReposition($"DPI changed {oldDpi.PixelsPerDip:0.##} -> {newDpi.PixelsPerDip:0.##}");
    }

    protected override void OnClosed(EventArgs e)
    {
        _foregroundWatcher?.Dispose();
        _source?.RemoveHook(WndProc);
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_MOUSEACTIVATE:
                // Clicking the island must not steal focus from the app the user is typing in.
                handled = true;
                return (IntPtr)PInvoke.MA_NOACTIVATE;

            case PInvoke.WM_DISPLAYCHANGE:
                ScheduleReposition("display settings changed");
                break;

            case PInvoke.WM_SETTINGCHANGE when (uint)wParam == (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA:
                ScheduleReposition("work area changed");
                break;

            case var m when m == TaskbarCreatedMessage:
                ScheduleReposition("Explorer restarted");
                break;
        }

        return IntPtr.Zero;
    }

    private void OnForegroundChanged()
    {
        // Another window came to the front; make sure it didn't cover the island.
        if (IsVisible && _source is not null)
        {
            NativeWindow.ReassertTopmost(Handle);
        }
    }

    /// <summary>Coalesces bursts of display, DPI and work-area notifications into one reposition.</summary>
    private void ScheduleReposition(string reason)
    {
        if (_repositionPending)
        {
            return;
        }

        _repositionPending = true;
        Dispatcher.InvokeAsync(
            () =>
            {
                _repositionPending = false;
                Reposition(reason);
            },
            DispatcherPriority.Background);
    }

    private void Reposition(string reason)
    {
        if (_source is null)
        {
            return;
        }

        var monitor = Monitors.Primary();
        var bounds = IslandPlacement.ComputeWindowBounds(monitor.WorkArea, monitor.DpiScale);
        NativeWindow.SetBoundsTopmost(Handle, bounds);
        Logger.Information(
            "Placed island at {Bounds} ({Reason}; work area {WorkArea}, scale {Scale:P0})",
            bounds,
            reason,
            monitor.WorkArea,
            monitor.DpiScale);
    }
}
