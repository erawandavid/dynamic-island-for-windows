using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Pulau.Core;
using Pulau.Modules.Debug;
using Pulau.ViewModels;
using Serilog;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Pulau;

/// <summary>The notification-area icon and its menu.</summary>
internal sealed class TrayIconController : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TrayIconController>();

    private readonly IslandViewModel _island;
    private readonly MenuItem _toggleIslandItem;
    private readonly System.Drawing.Icon _iconImage;
    private readonly TaskbarIcon _icon;

    /// <param name="island">The island the menu shows and hides.</param>
    /// <param name="quit">Quits the app.</param>
    /// <param name="debug">When set (Debug builds), adds a Debug submenu with dummy activities.</param>
    public TrayIconController(IslandViewModel island, Action quit, DebugModule? debug)
    {
        _island = island;

        _toggleIslandItem = new MenuItem();
        _toggleIslandItem.Click += (_, _) => ToggleIsland();

        // Placeholder until the settings window exists (Phase 5).
        var settingsItem = new MenuItem { Header = "Settings…", IsEnabled = false };

        var logsItem = new MenuItem { Header = "Open logs folder" };
        logsItem.Click += (_, _) => OpenLogsFolder();

        var quitItem = new MenuItem { Header = $"Quit {ProductInfo.Name}" };
        quitItem.Click += (_, _) => quit();

        var menu = new ContextMenu();
        menu.Items.Add(_toggleIslandItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(logsItem);
        if (debug is not null)
        {
            menu.Items.Add(CreateDebugMenu(island, debug));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);
        menu.Opened += (_, _) => UpdateToggleHeader();

        _iconImage = LoadIcon();
        _icon = new TaskbarIcon
        {
            ToolTipText = ProductInfo.Name,
            Icon = _iconImage,
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.LeftOrRightClick,
        };

        // Not placed in a visual tree, so it must be created explicitly. Efficiency Mode stays off:
        // it would throttle the whole process, including the island's animations.
        _icon.ForceCreate(false);
        UpdateToggleHeader();
    }

    public void Dispose()
    {
        _icon.Dispose();
        _iconImage.Dispose();
    }

    private static MenuItem CreateDebugMenu(IslandViewModel island, DebugModule debug)
    {
        var menu = new MenuItem { Header = "Debug" };
        AddItem("Show dummy activity (compact)", () => debug.ShowPersistent());
        AddItem("Show dummy transient (2 s)", debug.ShowTransient);
        AddItem("Peek dummy activity (3 s)", () => debug.ShowPersistent(requestPeek: true));
        AddItem("Clear dummy activity (idle)", debug.Clear);
        AddItem("Toggle expanded", island.ToggleExpanded);
        return menu;

        void AddItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Pulau.ico"))
            ?? throw new InvalidOperationException("Tray icon resource is missing.");
        using var stream = resource.Stream;

        // Pick the frame that matches the small-icon size for the current DPI (16 px at 100%).
        var size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
        return new System.Drawing.Icon(stream, size, size);
    }

    private void ToggleIsland()
    {
        var hide = !_island.IsUserHidden;
        _island.SetUserHidden(hide);
        Logger.Information(hide ? "Island hidden from the tray menu" : "Island shown from the tray menu");
        UpdateToggleHeader();
    }

    private void UpdateToggleHeader() =>
        _toggleIslandItem.Header = _island.IsUserHidden ? "Show island" : "Hide island";

    private static void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Logs);
            Process.Start(new ProcessStartInfo(AppPaths.Logs) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not open the logs folder {Path}", AppPaths.Logs);
        }
    }
}
