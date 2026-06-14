using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OledRefresher;

/// <summary>
/// The resident controller: owns the tray icon, the scheduling state machine and the
/// blackout/prompt lifecycle.
///
/// Decision logic, evaluated every few seconds:
///   • Not yet due and not overdue        → do nothing.
///   • Due (or overdue) and user is idle  → run the blackout silently.
///   • Due and user is active             → show the Run / Snooze prompt.
///   • Overdue (past the hard deadline)   → show the forced prompt that runs automatically.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _scheduler;
    private readonly BlackoutController _blackout;
    private readonly HotkeyWindow _hotkeys;

    private DateTime _lastRefreshUtc = DateTime.UtcNow;
    private DateTime _nextAttemptUtc;
    private DateTime _suppressForcedUntilUtc = DateTime.MinValue;
    private DateTime _pausedUntilUtc = DateTime.MinValue;
    private RefreshPromptForm? _openPrompt;
    private bool _busy;

    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _pauseItem = null!;
    private ToolStripMenuItem _startupItem = null!;

    public TrayApplicationContext(AppConfig config)
    {
        _config = config;
        _blackout = new BlackoutController(config);
        _nextAttemptUtc = DateTime.UtcNow + _config.Interval;

        _tray = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Visible = true,
            Text = "OLED Refresher",
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => RefreshNow();

        _hotkeys = new HotkeyWindow();
        _hotkeys.HotkeyPressed += (_, _) => RefreshNow();
        _hotkeys.TryRegister(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, (uint)Keys.B);

        _scheduler = new System.Windows.Forms.Timer { Interval = 5000 };
        _scheduler.Tick += OnSchedulerTick;
        _scheduler.Start();

        UpdateMenuState();
        _tray.ShowBalloonTip(4000, "OLED Refresher",
            "Running in the tray. Right-click for options.", ToolTipIcon.Info);
        Logger.Info("Tray context initialized.");
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Status") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Refresh now", null, (_, _) => RefreshNow()));

        _pauseItem = new ToolStripMenuItem("Pause for 1 hour", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);

        menu.Items.Add(new ToolStripSeparator());

        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = _config.StartWithWindows,
        };
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripMenuItem("Edit settings…", null, (_, _) => OpenInNotepad(AppPaths.ConfigFile)));
        menu.Items.Add(new ToolStripMenuItem("Reload settings", null, (_, _) => ReloadSettings()));
        menu.Items.Add(new ToolStripMenuItem("View log", null, (_, _) => OpenInNotepad(AppPaths.LogFile)));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("About", null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        return menu;
    }

    private void OnSchedulerTick(object? sender, EventArgs e)
    {
        try
        {
            UpdateMenuState();

            if (_busy || _blackout.IsRunning || _openPrompt is not null)
                return;

            var now = DateTime.UtcNow;
            if (now < _pausedUntilUtc)
                return;

            var since = now - _lastRefreshUtc;
            bool overdue = since >= _config.MaxSinceRefresh && now >= _suppressForcedUntilUtc;
            bool due = now >= _nextAttemptUtc;
            if (!overdue && !due)
                return;

            bool isIdle = IdleDetector.GetIdleTime() >= _config.IdleThreshold;
            if (isIdle)
            {
                StartBlackout();
                return;
            }

            ShowPrompt(overdue, since);
        }
        catch (Exception ex)
        {
            Logger.Error("Scheduler tick failed", ex);
        }
    }

    private void ShowPrompt(bool forced, TimeSpan since)
    {
        var prompt = new RefreshPromptForm(_config, forced, since);
        _openPrompt = prompt;

        prompt.Decided += (_, result) =>
        {
            _openPrompt = null;
            var now = DateTime.UtcNow;

            if (result == PromptResult.RunNow)
            {
                StartBlackout();
            }
            else // explicit or automatic snooze
            {
                _nextAttemptUtc = now + _config.Snooze;
                if (forced && _config.AllowSnoozePastDeadline)
                    _suppressForcedUntilUtc = now + _config.Snooze;
                Logger.Info($"Refresh snoozed for {_config.SnoozeMinutes} min.");
            }

            UpdateMenuState();
        };

        prompt.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_openPrompt, prompt))
                _openPrompt = null;
        };

        prompt.Show();
    }

    private void StartBlackout()
    {
        if (_busy || _blackout.IsRunning)
            return;

        _busy = true;
        _blackout.Run(() =>
        {
            var now = DateTime.UtcNow;
            _lastRefreshUtc = now;
            _nextAttemptUtc = now + _config.Interval;
            _suppressForcedUntilUtc = DateTime.MinValue;
            _busy = false;
            UpdateMenuState();
        });
    }

    private void RefreshNow()
    {
        // Manual trigger blacks out immediately, regardless of idle/active state.
        if (_openPrompt is not null)
        {
            _openPrompt.Close();
            _openPrompt = null;
        }
        StartBlackout();
    }

    private void TogglePause()
    {
        if (DateTime.UtcNow < _pausedUntilUtc)
        {
            _pausedUntilUtc = DateTime.MinValue;
            Logger.Info("Resumed.");
        }
        else
        {
            _pausedUntilUtc = DateTime.UtcNow.AddHours(1);
            _nextAttemptUtc = _pausedUntilUtc + _config.Interval;
            Logger.Info("Paused for 1 hour.");
        }
        UpdateMenuState();
    }

    private void ToggleStartup()
    {
        _config.StartWithWindows = !_config.StartWithWindows;
        StartupManager.Apply(_config.StartWithWindows);
        _config.Save();
        UpdateMenuState();
    }

    private void ReloadSettings()
    {
        try
        {
            var fresh = AppConfig.Load();
            CopyInto(fresh, _config);
            _nextAttemptUtc = DateTime.UtcNow + _config.Interval;
            StartupManager.Apply(_config.StartWithWindows);
            UpdateMenuState();
            _tray.ShowBalloonTip(3000, "OLED Refresher", "Settings reloaded.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to reload settings", ex);
        }
    }

    private static void CopyInto(AppConfig from, AppConfig to)
    {
        to.IntervalMinutes = from.IntervalMinutes;
        to.OverlaySeconds = from.OverlaySeconds;
        to.IdleThresholdSeconds = from.IdleThresholdSeconds;
        to.SnoozeMinutes = from.SnoozeMinutes;
        to.MaxMinutesSinceRefresh = from.MaxMinutesSinceRefresh;
        to.ForcedCountdownSeconds = from.ForcedCountdownSeconds;
        to.PromptAutoSnoozeSeconds = from.PromptAutoSnoozeSeconds;
        to.AllowSnoozePastDeadline = from.AllowSnoozePastDeadline;
        to.UseMinimizeFallback = from.UseMinimizeFallback;
        to.DismissOnInput = from.DismissOnInput;
        to.InputGraceMilliseconds = from.InputGraceMilliseconds;
        to.BlackoutAllMonitors = from.BlackoutAllMonitors;
        to.StartWithWindows = from.StartWithWindows;
    }

    private void UpdateMenuState()
    {
        var now = DateTime.UtcNow;
        bool paused = now < _pausedUntilUtc;

        _pauseItem.Text = paused
            ? $"Resume (paused, {Math.Max(1, (int)Math.Ceiling((_pausedUntilUtc - now).TotalMinutes))}m left)"
            : "Pause for 1 hour";
        _startupItem.Checked = _config.StartWithWindows;

        string status;
        if (paused)
            status = "Paused";
        else if (_blackout.IsRunning)
            status = "Refreshing…";
        else
        {
            int mins = Math.Max(0, (int)Math.Round((_nextAttemptUtc - now).TotalMinutes));
            status = $"Next refresh ~{mins} min";
        }

        _statusItem.Text = status;
        _tray.Text = $"OLED Refresher — {status}";
    }

    private static void OpenInNotepad(string path)
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            if (!File.Exists(path))
                File.WriteAllText(path, string.Empty);

            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not open {path}", ex);
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "OLED Refresher\n\n" +
            "Periodically blacks out the screen for a couple of seconds to exercise OLED pixels " +
            "and reduce static-UI burn-in during long gaming sessions.\n\n" +
            "• Idle    → the refresh runs automatically\n" +
            "• Active  → you get a Run / Snooze prompt\n" +
            "• Overdue → a forced protective refresh\n\n" +
            "Hotkey: Ctrl+Alt+B = refresh now.   Esc during a blackout cancels it.\n\n" +
            "Settings file:\n" + AppPaths.ConfigFile,
            "About OLED Refresher", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExitApp()
    {
        Logger.Info("Exit requested from tray.");
        _scheduler.Stop();
        _hotkeys.Dispose();
        _tray.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scheduler.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
