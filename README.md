# OLED Refresher

A small Windows tray app that periodically blacks out the screen for a couple of seconds to
exercise OLED pixels and reduce **static-UI burn-in** during long gaming sessions (fixed HUDs,
health bars, minimaps, score panels, etc.).

On an OLED panel, **black means the pixels are physically off** — so a brief full-black screen
is what actually *rests* the panel. (Minimizing to the desktop only shows your wallpaper and
taskbar, which are still lit and themselves static, so it does little for burn-in. This app uses
a true black overlay, with an optional minimize fallback for stubborn full-screen games.)

> Built for home use on an OLED TV/monitor. No telemetry, no network access, no admin rights.

---

## How it works

A resident tray process keeps its own schedule and watches whether you're actively using the PC:

| Situation | Behavior |
|-----------|----------|
| **Idle** (no input for a while) | The black refresh runs **automatically** — no interruption. |
| **Active** (you're playing) | A small **Run now / Snooze** toast appears in the corner; if ignored it quietly auto-snoozes. |
| **Overdue** (panel hasn't been rested for too long) | A **forced** refresh: a short countdown, then it runs automatically to protect the panel before burn-in can set in. |

Safety/finish details:

- The black screen lasts ~**2 seconds** (configurable), across **all monitors** by default.
- **Any key or mouse movement** dismisses the blackout early (so you're never stuck — and active
  play isn't disrupted). **`Esc` always cancels**, even if input-dismiss is turned off.
- **`Ctrl+Alt+B`** triggers a refresh on demand; double-clicking the tray icon does too.
- An **overlay + minimize fallback**: if the foreground app is a full-screen game, it's minimized
  first (so the black truly covers exclusive-fullscreen titles) and restored afterward.

The schedule is stateful (last-refresh time, snooze, and the hard deadline), which is why it's a
resident tray app rather than a stateless Task Scheduler script — the "snooze, but force a refresh
before burn-in" logic needs to remember state between firings. It auto-starts at sign-in via the
standard per-user **Run** key, so it's still "the OS starts it" with no babysitting.

---

## Requirements

- **Windows 10 or 11.**
- To **build**: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
  The published EXE is **self-contained**, so the *target* PC needs nothing installed.

> ⚠️ This is a Windows-only WinForms app. It can't be compiled or run on Linux/macOS — build it on
> the Windows machine you'll run it on (or any Windows box / Windows CI).

---

## Build & install (quick start)

From a PowerShell prompt in the repo root:

```powershell
# 1. Build a single self-contained EXE -> .\publish\OledRefresher.exe
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1

# 2. Install for your user + start at sign-in + launch it now
powershell -ExecutionPolicy Bypass -File .\build\Install-OledRefresher.ps1
```

Look for the tray icon near the clock. Right-click it for options.

To remove it:

```powershell
powershell -ExecutionPolicy Bypass -File .\build\Uninstall-OledRefresher.ps1            # keep settings
powershell -ExecutionPolicy Bypass -File .\build\Uninstall-OledRefresher.ps1 -RemoveSettings
```

### Manual build (no scripts)

```powershell
dotnet publish .\src\OledRefresher\OledRefresher.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o .\publish
```

---

## Tray menu

- **Refresh now** — black out immediately.
- **Pause for 1 hour / Resume** — temporarily stop scheduled refreshes.
- **Start with Windows** — toggle the run-at-sign-in entry.
- **Edit settings…** — opens `config.json` in Notepad.
- **Reload settings** — apply changes without restarting.
- **View log** — open the log file.
- **About / Exit**.

---

## Configuration

Settings live in **`%APPDATA%\OledRefresher\config.json`** (created on first run). Edit it, then use
**Reload settings**. Values are clamped to safe ranges.

| Setting | Default | Meaning |
|---|---|---|
| `IntervalMinutes` | `15` | How often a refresh is attempted. |
| `OverlaySeconds` | `2` | How long the screen stays black. |
| `IdleThresholdSeconds` | `60` | Idle for at least this long ⇒ refresh runs silently. |
| `SnoozeMinutes` | `30` | How far "Snooze" pushes the next attempt. |
| `MaxMinutesSinceRefresh` | `60` | Hard deadline: once this long since the last refresh, escalate to a forced refresh. |
| `ForcedCountdownSeconds` | `10` | Countdown shown before a forced refresh runs automatically. |
| `PromptAutoSnoozeSeconds` | `30` | If the normal prompt is ignored this long, it auto-snoozes. |
| `AllowSnoozePastDeadline` | `false` | `true` lets you snooze even a forced refresh (less protective). |
| `UseMinimizeFallback` | `true` | Minimize a full-screen foreground app before the overlay, then restore it. |
| `DismissOnInput` | `true` | Any key/mouse movement ends the blackout early (`Esc` always does). |
| `InputGraceMilliseconds` | `400` | Ignore input briefly after the overlay appears (so the launching click doesn't instantly close it). |
| `BlackoutAllMonitors` | `true` | Black out every display (`false` = primary only). |
| `StartWithWindows` | `true` | Launch automatically at sign-in. |

### Suggested tweaks

- **Single-player / pausable games:** raise protection by lowering `MaxMinutesSinceRefresh` (e.g. `45`).
- **Competitive / twitchy games:** keep `DismissOnInput: true` so a stray refresh never costs you a
  fight, and lean on the forced backstop to guarantee periodic rest.
- **Only play borderless-windowed games?** Set `UseMinimizeFallback: false` to avoid the brief
  minimize/restore flicker — the overlay alone already covers borderless titles.

---

## Notes & limitations

- **Exclusive-fullscreen games:** a topmost overlay can't always draw over true exclusive
  fullscreen, which is why the minimize fallback exists. Most modern games use borderless-windowed
  fullscreen, where the overlay covers cleanly. Restoring an exclusive-fullscreen game can take a
  second to re-initialize.
- **Anti-cheat:** the app only shows a normal black window and minimizes/restores the game window —
  benign operations. It injects nothing and reads no game memory. Risk is low, but if you run a
  title with strict kernel anti-cheat, test it in a safe moment first.
- **HDR / variable refresh rate:** unaffected; the overlay is just a black window.
- **Burn-in reality check:** this reduces *risk* from static elements by periodically cycling the
  pixels; it complements (doesn't replace) your TV's built-in pixel-shift and panel-refresh
  features. Keep those enabled too.

---

## Project layout

```
src/OledRefresher/        # .NET 8 WinForms tray app (C#)
  Program.cs              # entry point, single-instance, DPI/exception setup
  TrayApplicationContext  # tray icon + scheduling state machine
  BlackoutController      # runs a blackout: overlay, minimize fallback, input dismiss
  BlackoutForm            # the black, top-most, per-display window
  RefreshPromptForm       # the active/forced Run-or-Snooze toast
  IdleDetector            # system idle time (GetLastInputInfo)
  StartupManager          # run-at-sign-in (HKCU Run key)
  HotkeyWindow            # global Ctrl+Alt+B
  AppConfig / AppPaths / Logger / TrayIconFactory / NativeMethods
build/                    # publish + install/uninstall PowerShell scripts
```

## License

MIT — see [LICENSE](LICENSE).
