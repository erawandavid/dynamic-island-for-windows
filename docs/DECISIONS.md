# Decision log

Small decisions that `PLAN.md` doesn't answer, with the reasoning. Newest last. Big or hard-to-reverse decisions are discussed with the owner first.

| # | Date | Phase | Decision | Why |
|---|---|---|---|---|
| 1 | 2026-10-21 | 0 | The repository root is the project root (no `pulau/` subfolder); the GitHub repo is `dynamic-island-for-windows`, the product is still called "Pulau". | The plan's `pulau/` folder is just the clone directory. |
| 2 | 2026-10-21 | 0 | Solution file is `Pulau.slnx` (the XML solution format, default for `dotnet new sln` in .NET 10). | Readable, merge-friendly; supported by the .NET 10 SDK, Visual Studio 2022 17.13+/2026, Rider and C# Dev Kit. |
| 3 | 2026-10-21 | 0 | `Pulau.App` uses `AssemblyName=Pulau` and `RootNamespace=Pulau`. | The shipped executable is `Pulau.exe`, and the `App` class is `Pulau.App` instead of the awkward `Pulau.App.App`. |
| 4 | 2026-10-21 | 0 | `Pulau.App` sets `EnableWindowsTargeting=true`. | The whole solution compiles on Linux/macOS (contributors, cloud agents); the app still only runs on Windows. |
| 5 | 2026-10-21 | 0 | Tests use xUnit v3 (`xunit.v3`) on Microsoft.Testing.Platform, opted in via `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`. | Current stable xUnit line; on the .NET 10 SDK `dotnet test` needs the MTP opt-in for it. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` stay for IDE test explorers that still use VSTest. |
| 6 | 2026-10-21 | 0 | `global.json` pins `10.0.100` with `rollForward: latestFeature`. | Any .NET 10 SDK (10.0.1xx or newer feature band) works; .NET 11 does not get picked silently. |
| 7 | 2026-10-21 | 0 | Default version is `0.1.0-dev` (`VersionPrefix`/`VersionSuffix` in `Directory.Build.props`). | Local builds are clearly marked as dev; release builds pass `-p:Version=` from the git tag (Phase 6). |
| 8 | 2026-10-21 | 0 | Phase 0 app is a placeholder `Views/MainWindow` that says "Pulau is running (empty shell)". | Proves the app starts; it is replaced by `IslandWindow` in Phase 1. |
| 9 | 2026-10-21 | 0 | CI uses `actions/checkout@v5` and `actions/setup-dotnet@v5` with `global-json-file`, read-only `contents` permission, and cancels superseded runs. | Current major versions (Node 24); SDK version comes from one place. |
| 10 | 2026-10-21 | 0 | An interactive HTML mockup of the island lives in `docs/mockup/index.html`. It is a design reference only (not shipped), self-contained and makes no network requests. | Lets the owner and contributors try the states, sizes and spring parameters before the WPF island exists (Phases 1–2). Spring values that feel right there feed the Phase 2 defaults. |
