# Adding a module

A **module** is a source of things for the island to show: media, volume, battery, a timer… This guide walks through a small, complete example. Read [ARCHITECTURE.md](ARCHITECTURE.md) first for the big picture.

## How modules fit in

```
YourModule ──ActivityChanged──► ActivityManager ──► IslandViewModel ──► IslandWindow
  (App)        IslandActivity      (Core)              (App)              (DataTemplate → your view)
```

- A module raises `ActivityChanged` with an `IslandActivity` to show something, again to replace it, and with `null` to remove it. Each module has **at most one activity** at a time.
- The `ActivityManager` picks what is shown: the highest `Priority` wins, ties go to the most recent activity. A `Transient` activity disappears by itself after its `Duration`.
- An activity carries two **view models**: one for the compact pill and, optionally, one for the expanded island (`null` shows the home panel instead). WPF finds the view for each view-model type through a `DataTemplate`.

## Example: a greeting when Pulau starts

The example module shows "Hello!" for three seconds when Pulau starts, and a bigger greeting if you hover the island during that time.

### 1. The module

Create `src/Pulau.App/Modules/Greeting/GreetingModule.cs`:

```csharp
using Pulau.Core.Activities;
using Pulau.Core.Layout;

namespace Pulau.Modules.Greeting;

public sealed class GreetingModule : IIslandModule
{
    public const string ModuleId = "greeting";

    public event EventHandler<IslandActivity?>? ActivityChanged;

    public string Id => ModuleId;

    public string DisplayName => "Greeting";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ActivityChanged?.Invoke(this, new IslandActivity(
            ModuleId,
            ActivityPriority.Normal,
            ActivityKind.Transient,
            Duration: TimeSpan.FromSeconds(3),
            CompactViewModel: new GreetingViewModel("Hello!"),
            ExpandedViewModel: new GreetingViewModel("Hello from your new module!"))
        {
            // Compact content may use 220–280 DIPs of width; expanded content up to 380 × 140.
            CompactSize = new IslandSize(220, 34, 17),
        });

        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;
}

public sealed class GreetingViewModel(string text)
{
    public string Text { get; } = text;
}
```

Note that while the island is expanded, transient activities are only shown if their priority is `Critical`, so with `Normal` priority the expanded greeting only appears if the island was already expanded for another reason. Use `ActivityKind.Persistent` (and no `Duration`) for something that should stay until you remove it.

### 2. The view

Create `src/Pulau.App/Modules/Greeting/GreetingView.xaml` (a `UserControl`; its `DataContext` is the view model):

```xml
<UserControl x:Class="Pulau.Modules.Greeting.GreetingView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <TextBlock Text="{Binding Text}"
               HorizontalAlignment="Center"
               VerticalAlignment="Center"
               FontSize="13"
               TextTrimming="CharacterEllipsis" />
</UserControl>
```

and its code-behind `GreetingView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace Pulau.Modules.Greeting;

public partial class GreetingView : UserControl
{
    public GreetingView()
    {
        InitializeComponent();
    }
}
```

The view fills a box of exactly the activity's size (e.g. 220 × 34). Text is white with Segoe UI Variable by default. For colors, fonts and buttons, use the resources in `Views/IslandTheme.xaml` (`IslandSecondaryText`, `IslandIconButton`, …) with `{StaticResource …}`.

### 3. The DataTemplate

Add to `src/Pulau.App/Views/IslandTemplates.xaml` (and the `xmlns:greeting` line at the top):

```xml
xmlns:greeting="clr-namespace:Pulau.Modules.Greeting"

<DataTemplate DataType="{x:Type greeting:GreetingViewModel}">
    <greeting:GreetingView />
</DataTemplate>
```

If your compact and expanded content need different views, use two view-model types (the media module has `MediaCompactViewModel` and `MediaExpandedViewModel` sharing one `NowPlayingViewModel`).

### 4. Register the module

In `App.ConfigureServices` (`src/Pulau.App/App.xaml.cs`):

```csharp
AddModule<GreetingModule>(services);
```

Constructor parameters are injected: ask for `TimeProvider`, `Dispatcher` or other registered services.

### 5. Try it

`dotnet run --project src/Pulau.App` — the pill widens with "Hello!" for three seconds.

## Rules of the road

- **UI thread only.** Raise `ActivityChanged` and touch view models on the UI thread. Events from Windows APIs (WinRT, Core Audio, …) usually arrive on background threads: hand them over with `Dispatcher.InvokeAsync`, as `MediaModule` does.
- **Never throw from callbacks.** Catch and log (`Serilog.Log.ForContext<YourModule>()`). If `StartAsync` throws, the `ActivityManager` disables your module and the rest of Pulau keeps running.
- **Time goes through `TimeProvider`.** It is injected; its timers already fire on the UI thread. This keeps your logic testable with `FakeTimeProvider`.
- **Put logic in `Pulau.Core`** when it doesn't need WPF or WinRT (choosing, timing, formatting), and unit-test it in `Pulau.Core.Tests`.
- **Cheap when idle.** No polling loops; start timers in `IIslandContent.OnShown` and stop them in `OnHidden` if your content only needs them while visible.
- **Buttons** inside the island use the `IslandIconButton` style (it is not focusable, so clicking doesn't steal focus) and need an `AutomationProperties.Name`.
- **Auto-peek:** set `RequestsPeek = true` on an activity to expand the island briefly (if it is compact and showing your activity), e.g. when something meaningful changes.
- **Click action:** set `ClickAction` on an activity to decide what a click on the island does while your activity is shown (the media module switches to the app that is playing). The island collapses afterwards. Without it, a click expands or collapses the island.
- **No network access, no telemetry, no admin rights.**
- Add manual test steps to [MANUAL_TESTS.md](MANUAL_TESTS.md) and a line to the *Unreleased* section of [CHANGELOG.md](../CHANGELOG.md).
