# Contributing to Pulau

Thanks for your interest in Pulau! Bug reports, ideas, docs fixes and code are all welcome.

## Before you start

- Check the [open issues](https://github.com/erawandavid/dynamic-island-for-windows/issues) to avoid duplicate work. For anything bigger than a small fix, open an issue first so we can agree on the approach.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the pieces fit together, and [PLAN.md](PLAN.md) for the roadmap.
- Want to add a new island module (timer, Bluetooth, …)? See [docs/ADDING_A_MODULE.md](docs/ADDING_A_MODULE.md).

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Use Visual Studio (".NET desktop development" workload), VS Code with C# Dev Kit, or Rider.
3. Build and test:

   ```powershell
   dotnet build -c Release
   dotnet test -c Release
   dotnet run --project src/Pulau.App
   ```

The app runs on Windows only. On Linux/macOS you can still build the solution and run the `Pulau.Core` tests.

## Guidelines

- **Language:** code, comments, commit messages and docs are in English.
- **Original code only.** Do not copy code from other Dynamic Island projects or anywhere else with an incompatible license. Pulau is MIT; several similar projects are GPL-3.0 or CC BY-SA. Taking inspiration from features is fine; copying code is not.
- **Keep Core pure.** `Pulau.Core` must not reference WPF or WinRT. Put logic there whenever you can and cover it with unit tests.
- **Time-based logic** takes a `TimeProvider` so it can be tested with `FakeTimeProvider`.
- **Win32 interop** goes through [CsWin32](https://github.com/microsoft/CsWin32) (`NativeMethods.txt`).
- **No network access, telemetry or admin rights.** These are hard rules for the project.
- **Accessibility:** interactive controls need an `AutomationProperties.Name`.
- Follow `.editorconfig`. Release builds treat warnings as errors.
- New NuGet packages: latest stable version only, added with `dotnet add package`.

## Commits and pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `ci:`.
- Keep pull requests focused on one change. Fill in the PR template.
- Add an entry to the *Unreleased* section of [CHANGELOG.md](CHANGELOG.md) for user-visible changes.
- CI must be green: the solution builds in Release and all tests pass.
- For UI changes, describe how you tested them manually (Windows version, display scaling, monitors), and add steps to [docs/MANUAL_TESTS.md](docs/MANUAL_TESTS.md) when relevant.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
