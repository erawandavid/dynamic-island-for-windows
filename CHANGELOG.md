# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Repository foundation: `Pulau.slnx` solution with `Pulau.Core`, `Pulau.App` (empty WPF shell) and `Pulau.Core.Tests`.
- Shared build settings (`Directory.Build.props`), pinned .NET 10 SDK (`global.json`), `.editorconfig`, `.gitattributes`, `.gitignore`.
- Documentation: README, CONTRIBUTING, architecture overview, decision log, manual test checklist.
- GitHub Actions CI (build + test on Windows), issue forms and pull request template.
- The island window: a black pill at the top center of the primary monitor's work area. It stays on top, is not in the taskbar or Alt+Tab, never takes focus, and follows display, DPI and work-area changes and Explorer restarts.
- Tray icon with Show/Hide island, Settings (placeholder), Open logs folder and Quit.
- Single-instance guard, file logging to `%LOCALAPPDATA%\Pulau\logs` (daily, 7 files kept) and logging of all unhandled exceptions.
- Per-Monitor V2 DPI awareness (`app.manifest`) and an app icon.
- Animated island: a spring-driven morph between the idle pill, compact activities and the expanded panel, with content that fades out and back in. Hover for 150 ms or click to expand; it collapses 400 ms after the pointer leaves. Respects the Windows "Animation effects" setting, and the render loop only runs while something moves.
- Home panel when expanded with no activity: clock, date and (on laptops) battery level and charging status, in the system's regional format.
- Hiding and showing the island from the tray now animates.
- Debug builds: a tray **Debug** menu to show dummy compact, transient and peeking activities.

[Unreleased]: https://github.com/erawandavid/dynamic-island-for-windows/commits/main
