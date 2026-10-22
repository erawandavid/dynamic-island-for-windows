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
| 0.3 | `dotnet run --project src/Pulau.App -c Release` | The app starts. (Since Phase 1 it shows the island instead of the Phase 0 placeholder window; see Phase 1.) |
| 0.5 | Push the branch / open a PR to `main`. | The **CI** workflow is green. |

## Phase 1 — Island window

Start the app with `dotnet run --project src/Pulau.App -c Release` (the terminal stays busy while Pulau runs), or run `src\Pulau.App\bin\Release\net10.0-windows10.0.19041.0\Pulau.exe` after a build.

Please report: Windows version (`winver`), display scaling, number of monitors, and pass/fail for each row. Attach the log from step 1.12 if anything fails.

| # | Steps | Expected |
|---|---|---|
| 1.1 | Start Pulau. | A small black pill (about 120 × 32 at 100% scaling) appears at the top center of the screen, 8 px below the top edge (below the taskbar if the taskbar is at the top). No window frame or title bar. |
| 1.2 | Look at the taskbar. | No Pulau button on the taskbar. A pill-shaped icon is in the notification area (it may be in the `^` overflow). |
| 1.3 | Press Alt+Tab, then Win+Tab. | Pulau does not appear in either. |
| 1.4 | Open Notepad and start typing. While typing, click the pill once, then keep typing. | The text keeps going into Notepad. Notepad's title bar stays active. |
| 1.5 | **Click-through.** Maximize a browser so its tabs run under the top center of the screen. Click a tab (or anything) that is next to the pill: about 40 px to the left/right of it, and about 100 px below it. | The click reaches the browser. Only the pill itself (and its faint shadow) blocks clicks. Please report this one explicitly. |
| 1.6 | Click around other windows, maximize one, drag one over the top center. | The pill stays on top of normal windows. The Start menu may cover it; that is acceptable. |
| 1.7 | Settings → System → Display → Scale: try 100%, 125% and 150% (no sign-out needed). | After each change the pill is still centered at the top, sharp (not blurry), and its size scales with the rest of the UI. |
| 1.8 | With two monitors: Settings → System → Display → pick the other monitor → "Make this my main display". Also try monitors with different scaling. | The pill moves to the new main display, centered at the top, at the correct size. |
| 1.9 | Windows 10 only: unlock the taskbar and drag it to the top of the screen. | The pill moves down to sit 8 px below the taskbar. |
| 1.10 | Task Manager → Processes → Windows Explorer → Restart. | After Explorer comes back, the pill is still in place and the tray icon reappears. |
| 1.11 | Left-click or right-click the tray icon. Choose **Hide island**, then open the menu again and choose **Show island**. | The menu shows: Hide island, Settings… (greyed out), Open logs folder, Quit Pulau. The pill disappears, then comes back in the same place. |
| 1.12 | Tray menu → **Open logs folder**. | Explorer opens `%LOCALAPPDATA%\Pulau\logs` with a `pulau-YYYYMMDD.log` file containing a "starting" line and "Placed island at" lines. |
| 1.13 | While Pulau runs, start it a second time. | No second pill or tray icon; the second launch exits immediately. |
| 1.14 | Tray menu → **Quit Pulau**. | Pill and tray icon disappear; no `Pulau.exe` in Task Manager; the log ends with an "exiting" line. |
| 1.15 | Optional: watch Pulau in Task Manager for a minute while idle. | CPU stays at about 0%. |

## Phase 2 — Animation and states

Run a **Debug** build so the tray has a **Debug** menu: `dotnet run --project src/Pulau.App` (no `-c Release`).

Please also tell us how the motion *feels*: too slow, too fast, too bouncy?

| # | Steps | Expected |
|---|---|---|
| 2.1 | Start Pulau. | The small idle pill (about 120 × 32) at the top center. |
| 2.2 | Rest the pointer on the pill. | After a moment (~0.15 s) it grows smoothly, with a slight spring, into the home panel: clock and date on the left; on a laptop, battery % and status on the right. The text fades in near the end of the morph. |
| 2.3 | Move the pointer away. | About 0.4 s later it shrinks back to the idle pill. |
| 2.4 | Sweep the pointer quickly across the pill. | It does not expand. |
| 2.5 | Click the pill; then click it again without moving away; then move away and back. | First click: expands at once. Second click: collapses and stays collapsed while the pointer is still on it. After leaving and coming back, hovering expands it again. |
| 2.6 | Tray → Debug → **Show dummy activity (compact)**. Then hover. | The pill widens to the compact size with "Debug activity". Hovering expands it to a large panel with "Expanded debug activity". |
| 2.7 | Tray → Debug → **Show dummy transient (2 s)**. | "Transient (2 s)" shows for two seconds, then the previous content comes back. |
| 2.8 | With the dummy activity showing: Tray → Debug → **Peek dummy activity (3 s)**. | The island expands by itself for three seconds, then returns to compact. |
| 2.9 | Tray → Debug → **Clear dummy activity (idle)**. | Back to the idle pill. |
| 2.10 | Tray → Debug → **Toggle expanded**, move the pointer elsewhere, then toggle again. | It expands and stays expanded without the pointer; the second toggle collapses it. |
| 2.11 | Tray → **Hide island**, then **Show island**. | The pill fades out while shrinking upward, then comes back the same way. |
| 2.12 | Hover in and out quickly, click repeatedly, trigger debug items in a row. | Motion redirects smoothly mid-flight; no jumps, flicker or wrong content left behind. |
| 2.13 | Settings → Accessibility → Visual effects → **Animation effects** off; repeat 2.2. Turn it back on afterwards. | Changes are instant (no morph). |
| 2.14 | If you have a 120 Hz+ display, repeat 2.2 on it. | Still smooth. |
| 2.15 | Leave the island idle for a minute and watch Pulau in Task Manager. Then open the home panel across a minute change. | CPU about 0% when nothing moves. The clock updates when the minute changes. |
| 2.16 | Compare the date/time format with the taskbar clock. | Same regional format (e.g. 24-hour if Windows uses it). |
