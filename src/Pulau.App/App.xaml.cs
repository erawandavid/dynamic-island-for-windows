using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pulau.Core;
using Pulau.Core.Activities;
using Pulau.Core.States;
using Pulau.Modules.Debug;
using Pulau.Threading;
using Pulau.ViewModels;
using Pulau.Views;
using Serilog;

namespace Pulau;

public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private ServiceProvider? _services;

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
            _services = ConfigureServices();

            var activities = _services.GetRequiredService<ActivityManager>();
            activities.ModuleFailed += (_, failure) =>
                Log.Error(failure.Exception, "Module {Module} failed during {Phase}", failure.ModuleId, failure.Phase);
            foreach (var module in _services.GetServices<IIslandModule>())
            {
                activities.Register(module);
            }

            _services.GetRequiredService<IslandWindow>().Show();
            _services.GetRequiredService<TrayIconController>();
            _ = StartModulesAsync(activities);
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
            Log.Information("{Product} exiting with code {ExitCode}", ProductInfo.Name, e.ApplicationExitCode);
            try
            {
                // Disposes the tray icon and the modules.
                _services?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while shutting down");
            }

            Log.CloseAndFlush();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    private static async Task StartModulesAsync(ActivityManager activities)
    {
        try
        {
            await activities.StartAsync(CancellationToken.None);
            Log.Information(
                "Modules running: {Modules}",
                activities.Modules.Where(m => activities.IsEnabled(m.Id)).Select(m => m.Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Starting modules failed");
        }
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

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Log and keep running: a glitch in one part of the UI shouldn't take the island down.
        Log.Error(e.Exception, "Unhandled exception on the UI thread");
        e.Handled = true;
    }

    /// <summary>The composition root: every long-lived object, created once.</summary>
    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Dispatcher);
        services.AddSingleton<TimeProvider>(new DispatcherTimeProvider(Dispatcher));
        services.AddSingleton(sp => new IslandStateMachine(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new ActivityManager(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<IslandWindow>();

        // Island modules. Each one is registered as itself and as IIslandModule.
#if DEBUG
        AddModule<DebugModule>(services);
#endif

        services.AddSingleton(sp => new TrayIconController(
            sp.GetRequiredService<IslandViewModel>(),
            Quit,
            sp.GetService<DebugModule>()));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static void AddModule<TModule>(IServiceCollection services)
        where TModule : class, IIslandModule
    {
        services.AddSingleton<TModule>();
        services.AddSingleton<IIslandModule>(sp => sp.GetRequiredService<TModule>());
    }

    private void Quit()
    {
        _services?.GetService<IslandWindow>()?.Close();
        Shutdown();
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
}
