# Architecture

This document describes how Pulau is put together. It is the English version of section 3 of [PLAN.md](../PLAN.md); if they ever disagree, update both.

## Overview

```
┌────────────────────────── Pulau.App (WPF) ───────────────────────────┐
│  IslandWindow ── IslandViewModel ◄── ActivityManager ◄── Modules     │
│       │               │                (Core)            ├ Media     │
│  IslandAnimator   IslandStateMachine                     ├ Volume    │
│  (springs, Core)  (Core)                                 ├ Battery   │
│                                                          └ ...       │
│  TrayIcon · SettingsWindow · FullscreenWatcher · Interop (CsWin32)   │
└──────────────────────────────────────────────────────────────────────┘
Pulau.Core = pure logic (no WPF/WinRT), targets net10.0, 100% unit-testable
```

- **Pulau.Core** holds everything that can be expressed without Windows UI APIs: module abstractions, `ActivityManager`, `IslandStateMachine`, spring physics and the settings model. It targets plain `net10.0` and is covered by `Pulau.Core.Tests`.
- **Pulau.App** is the WPF application: the island and settings windows, views and view models, the modules that talk to Windows (SMTC, Core Audio, power), the tray icon, fullscreen detection, Win32 interop and the dependency-injection root.

## Repository layout

```
src/
  Pulau.Core/            module abstractions, ActivityManager, state machine, Spring, settings model
  Pulau.App/             WPF: entry point, windows, views, tray, DI root
    Modules/             MediaModule, VolumeModule, BatteryModule (+ ViewModel & View)
    Interop/             NativeMethods.txt + Win32 helpers
    Views/               IslandWindow, SettingsWindow, DataTemplates
    Assets/              original .ico icons (no Apple assets)
tests/
  Pulau.Core.Tests/
docs/
.github/
```

## State machine

States: `Hidden`, `Idle`, `Compact`, `Expanded`.

| From | Trigger | To |
|---|---|---|
| Idle | an activity appears | Compact |
| Compact | all activities are gone | Idle |
| Idle / Compact | hover ≥ 150 ms, click, or hotkey | Expanded |
| Expanded | mouse leaves for > 400 ms | Compact (if there is an activity) / Idle |
| Compact | track changes and *auto-peek* is enabled | Expanded for 3 s → back |
| Any | fullscreen detected / user hides via tray or hotkey | Hidden |
| Hidden | condition clears / user shows | previous state |

- While **Expanded**, *transient* activities (e.g. volume) are ignored unless their priority is `Critical`.
- Expanded with no activity shows the **Home** panel: clock, date (system format) and battery percentage on laptops.

## Activities and priorities

```csharp
public enum ActivityPriority { Low, Normal, High, Critical }
public enum ActivityKind { Persistent, Transient }

public sealed record IslandActivity(
    string ModuleId,
    ActivityPriority Priority,
    ActivityKind Kind,
    TimeSpan? Duration,          // required for Transient
    object CompactViewModel,     // mapped to a View through a DataTemplate in App
    object? ExpandedViewModel);

public interface IIslandModule
{
    string Id { get; }
    string DisplayName { get; }
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
    event EventHandler<IslandActivity?> ActivityChanged;  // null = activity removed
}
```

`ActivityManager` rules:

- The highest-priority activity is shown; ties go to the most recent one.
- A transient activity overrides persistent activities of equal or lower priority for its `Duration`, after which the persistent activity is shown again.
- All time-based logic uses `TimeProvider`, so it can be faked in tests.
- A module that throws is isolated: the exception is logged, the module is disabled, and the app keeps running.

## Animation

- Custom **spring** physics in Core (`Spring.Step(dt)`) rather than WPF's built-in easing, so the morph feels alive. Parameters: stiffness and damping ratio (starting around 0.8 — slightly bouncy — and tuned by hand).
- Animated properties: `Width`, `Height`, `CornerRadius`, plus content opacity/scale.
- The render loop runs on `CompositionTarget.Rendering` **only while a spring is still moving** — at rest there is no loop and CPU use is ~0%.
- Retargeting mid-animation keeps the current velocity; `dt` is clamped to avoid jumps after a stalled frame.
- Content transition: old content fades out quickly (~100 ms) → shape morphs → new content fades in near the end.
- Respects the Windows "Animation effects" setting (`UISettings.AnimationsEnabled`): when off, transitions are instant or very short.

Initial sizes (DIP; scalable later through settings):

| State | Size | Corner radius |
|---|---|---|
| Idle | 120 × 32 | 16 |
| Compact | 220–280 × 34 (depends on content) | 17 |
| Expanded (Home) | 340 × 110 | 28 |
| Expanded (Media) | 380 × 140 | 28 |

Visual style: background `#000000`, white text, secondary text `#A1A1A1`, Segoe UI Variable, 8 DIP from the top of the work area. Dark theme only in v1.

## Window approach

A single fixed-size transparent window (at least the largest expanded size plus shadow margin, e.g. 480 × 220 DIP) sits at the top center of the monitor; the pill is a `Border` inside it. Fully transparent pixels must be **click-through** — this is verified in Phase 1. If they are not, the fallback is to resize the window to follow the pill's bounds, or to toggle `WS_EX_TRANSPARENT` based on the cursor position. The outcome is recorded in [DECISIONS.md](DECISIONS.md).

How it is implemented (Phase 1):

- `Views/IslandWindow` is borderless, `AllowsTransparency`, `Topmost`, not in the taskbar and shown without activation. In `OnSourceInitialized` it adds `WS_EX_TOOLWINDOW` (no Alt+Tab) and `WS_EX_NOACTIVATE` (no focus), and its window procedure answers `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`.
- Placement is computed by `Pulau.Core.Layout.IslandPlacement` from the monitor's work area and effective DPI, in physical pixels, and applied with `SetWindowPos(HWND_TOPMOST)`. It is recomputed on `WM_DISPLAYCHANGE`, `WM_SETTINGCHANGE`/`SPI_SETWORKAREA`, DPI changes and the `TaskbarCreated` broadcast (Explorer restart); bursts are coalesced into one reposition.
- `Interop/ForegroundWatcher` hooks `EVENT_SYSTEM_FOREGROUND` with `SetWinEventHook` and re-asserts topmost whenever another window comes to the front. No polling.
- `App` owns startup: single-instance mutex, Serilog file logging, global exception handlers, the island window and `TrayIconController`.
