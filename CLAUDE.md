# CLAUDE.md

Pulau is a Dynamic Island for Windows (WPF, .NET 10). `PLAN.md` is the source of truth for scope, phases and technical decisions — read it first.

## Commands

```bash
dotnet build -c Release                 # whole solution (Pulau.slnx); warnings are errors in Release
dotnet test -c Release                  # Pulau.Core.Tests (xUnit v3 on Microsoft.Testing.Platform, see global.json)
dotnet run --project src/Pulau.App      # Windows only
```

The solution builds on Linux/macOS (`EnableWindowsTargeting`), but the app only runs on Windows.

## Layout

- `src/Pulau.Core/` — pure logic, `net10.0`, no WPF/WinRT. Module abstractions, ActivityManager, state machine, springs, settings model. Must stay 100% unit-testable.
- `src/Pulau.App/` — WPF app (`net10.0-windows10.0.19041.0`, assembly `Pulau`, root namespace `Pulau`): entry point, windows, tray, DI root.
  - `Modules/` (MediaModule, VolumeModule, BatteryModule + their ViewModels/Views), `Interop/` (CsWin32 `NativeMethods.txt` + Win32 helpers), `Views/` (windows, `IslandAnimator`, island theme and DataTemplates), `ViewModels/`, `Threading/`, `Assets/`.
- `tests/Pulau.Core.Tests/` — xUnit tests for Core.
- `docs/` — `ARCHITECTURE.md`, `ADDING_A_MODULE.md`, `DECISIONS.md`, `MANUAL_TESTS.md`.
- `tools/` — helper scripts (e.g. `make_icon.py` regenerates `Assets/Pulau.ico`).
- Shared build settings: `Directory.Build.props`. SDK pinned in `global.json`.

## Working rules

1. One phase per request, in order. Don't start the next phase until asked.
2. At the start of a phase, re-read that phase in `PLAN.md` and this file.
3. At the end of a phase:
   - `dotnet build -c Release` and `dotnet test -c Release` pass with no errors;
   - update `CHANGELOG.md` (*Unreleased*);
   - tick the phase checklist in `PLAN.md`;
   - commit using Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …). Don't push unless asked.
4. You can't see the screen. For UI work, write manual test steps in `docs/MANUAL_TESTS.md`, ask the owner to run them, then fix based on their report.
5. Small decisions not covered by `PLAN.md`: pick a sensible default and record it in `docs/DECISIONS.md`. Big or hard-to-reverse decisions: ask first.
6. Code, comments, commit messages, README and all public docs are in **English**. (Chatting with the owner in Indonesian is fine.)
7. Add NuGet packages with `dotnet add package <Name>` (latest stable). No prerelease versions.
8. **Never copy code from other Dynamic Island projects** — some are GPL-3.0 / CC BY-SA, incompatible with MIT. Feature ideas are fine, code is not.

## Conventions

- Nullable and implicit usings are on everywhere; file-scoped namespaces; private fields `_camelCase`.
- Time-based logic in Core takes a `TimeProvider` so tests can use `FakeTimeProvider`.
- Win32 P/Invoke goes through CsWin32 (`NativeMethods.txt`), not hand-written `DllImport`s.
- No network access, no telemetry, no admin rights — ever.
- Never name a source folder `Debug`, `Release`, `x64`, `x86`, `ARM`, `ARM64`, `bin`, `obj`, `log` or `logs`: `.gitignore` excludes them, so their files silently never get committed.
- Before pushing, build and test a fresh clone of the commit (`git clone . <tmp> && cd <tmp> && dotnet build -c Release && dotnet test -c Release`). Your working folder can hide missing or ignored files.
