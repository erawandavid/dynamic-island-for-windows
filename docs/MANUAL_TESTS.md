# Manual tests

Checks that can't be automated (UI, Windows integration). Run them on Windows before merging a phase that touches the UI, and report the result (pass/fail + notes, Windows version, display scaling, number of monitors).

Run the app from the repo root with:

```powershell
dotnet run --project src/Pulau.App -c Release
```

## Phase 0 — Foundation

| # | Steps | Expected |
|---|---|---|
| 0.1 | `dotnet build -c Release` | Build succeeds with 0 warnings, 0 errors. |
| 0.2 | `dotnet test -c Release` | All tests pass. |
| 0.3 | `dotnet run --project src/Pulau.App -c Release` | A small black window titled "Pulau" appears in the center of the screen with the text "Pulau is running (empty shell)." |
| 0.4 | Close the window. | The process exits (no `Pulau.exe` left in Task Manager). |
| 0.5 | Push the branch / open a PR to `main`. | The **CI** workflow is green. |
