using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Pulau.Core;
using Pulau.Views;
using Serilog;

namespace Pulau;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private IslandWindow? _island;
    private TrayIconController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            // Pulau is already running in this session; the second launch just exits.
            Shutdown();
            return;
        }

        ConfigureLogging();
        RegisterGlobalExceptionHandlers();
        base.OnStartup(e);

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Log.Information(
            "{Product} {Version} starting on {OS} ({Architecture})",
            ProductInfo.Name,
            version,
            Environment.OSVersion.VersionString,
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);

        try
        {
            _island = new IslandWindow();
            _island.Show();
            _tray = new TrayIconController(_island, Quit);
        }
        catch (Exception ex)
        {
            // Without the window or the tray icon there is no way to reach or quit the app,
            // so don't linger as an invisible process.
            Log.Fatal(ex, "Startup failed");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstance is not null)
        {
            _tray?.Dispose();
            Log.Information("{Product} exiting with code {ExitCode}", ProductInfo.Name, e.ApplicationExitCode);
            Log.CloseAndFlush();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    private void Quit()
    {
        _island?.Close();
        Shutdown();
    }

    private static void ConfigureLogging()
    {
        Directory.CreateDirectory(AppPaths.Logs);
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "pulau-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>Nothing fails silently: every unhandled exception ends up in the log.</summary>
    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Log and keep running: a glitch in one part of the UI shouldn't take the island down.
        Log.Error(e.Exception, "Unhandled exception on the UI thread");
        e.Handled = true;
    }
}
