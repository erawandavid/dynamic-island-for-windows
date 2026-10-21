# Pulau

**A Dynamic Island for Windows** — a small black pill at the top of your screen that shows what's playing, volume and battery changes, and gets out of the way when you don't need it.

[![CI](https://github.com/erawandavid/dynamic-island-for-windows/actions/workflows/ci.yml/badge.svg)](https://github.com/erawandavid/dynamic-island-for-windows/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> **Status:** early development. Nothing is released yet — see the [roadmap](PLAN.md) and [changelog](CHANGELOG.md).

<!-- TODO: replace with a demo GIF / screenshot before the first release. -->
_Demo GIF coming soon._ Until then, open [`docs/mockup/index.html`](docs/mockup/index.html) in a browser for an interactive design mockup of the island (not the real app).

## Features

Planned for v0.1:

- **Idle** — a tiny, unobtrusive pill at the top center of your screen.
- **Compact** — widens a little when something happens: music playing, volume changed, charger plugged in.
- **Expanded** — grows on hover or click: media controls, clock, date and battery.
- **Hidden** — gets out of the way automatically for fullscreen games and videos.
- Media from any app that talks to Windows media controls (Spotify, browsers, …): album art, title, artist, playback controls, progress.
- Smooth spring-based animations that respect the Windows "Animation effects" setting.
- Tray icon, global hotkey, start with Windows, multi-monitor and high-DPI support.

## Download

Releases are not available yet. Once they are:

1. Download `Pulau-vX.Y.Z-win-x64.zip` (or `-win-arm64.zip`) from [Releases](https://github.com/erawandavid/dynamic-island-for-windows/releases).
2. Extract it anywhere and run `Pulau.exe`.

No installer, no .NET install and no admin rights needed.

### System requirements

- Windows 10 version 1809 or later, or Windows 11
- x64 or ARM64

## Build from source

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the exact feature band is pinned in [`global.json`](global.json)).

```powershell
git clone https://github.com/erawandavid/dynamic-island-for-windows.git
cd dynamic-island-for-windows
dotnet build -c Release
dotnet test -c Release
dotnet run --project src/Pulau.App
```

The app itself only runs on Windows. The solution also compiles on Linux and macOS, and `Pulau.Core` tests run anywhere.

## Privacy

Pulau has no accounts, no cloud, no telemetry and makes **no network connections**. Nothing leaves your PC. Logs are written locally to `%LOCALAPPDATA%\Pulau\logs` so you can attach them to bug reports if you choose.

## Contributing

Contributions are welcome! Read [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) to get started.

## License

[MIT](LICENSE) © 2026 David Dylan Erawan

Pulau is an independent project and is not affiliated with Apple or Microsoft.
