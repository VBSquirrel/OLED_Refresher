using System;
using System.Drawing;
using System.Windows.Forms;

namespace OledRefresher;

internal enum PromptResult
{
    None,
    RunNow,
    Snooze,
}

/// <summary>
/// The toast shown when the user is active. Two modes:
///  • Normal  → "Run now" / "Snooze"; if ignored it auto-snoozes so it never lingers.
///  • Forced  → shown once the panel is overdue; counts down and then runs automatically
///              (snooze hidden unless <see cref="AppConfig.AllowSnoozePastDeadline"/>).
/// It is created as a non-activating top-most tool window so it does not steal focus
/// (and therefore input) from the game underneath.
/// </summary>
internal sealed class RefreshPromptForm : Form
{
    private readonly bool _forced;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _countdownLabel;
    private int _secondsLeft;

    public PromptResult Result { get; private set; } = PromptResult.None;
    public event EventHandler<PromptResult>? Decided;

    public RefreshPromptForm(AppConfig config, bool forced, TimeSpan sinceLastRefresh)
    {
        _forced = forced;

        var accent = forced ? Color.FromArgb(220, 80, 60) : Color.FromArgb(46, 196, 182);

        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.Gainsboro;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 360;
        Height = 150;
        StartPosition = FormStartPosition.Manual;

        Controls.Add(new Panel { Dock = DockStyle.Left, Width = 6, BackColor = accent });

        Controls.Add(new Label
        {
            Text = forced ? "OLED protection — refresh required" : "OLED refresh due",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = forced ? Color.FromArgb(255, 140, 120) : Color.White,
            AutoSize = false,
            Location = new Point(20, 14),
            Size = new Size(330, 26),
        });

        int mins = Math.Max(1, (int)Math.Round(sinceLastRefresh.TotalMinutes));
        Controls.Add(new Label
        {
            Text = forced
                ? $"Your panel hasn't been rested for ~{mins} min. A {config.OverlaySeconds}-second black refresh will run automatically."
                : "Time to rest your panel with a brief black screen.",
            AutoSize = false,
            Location = new Point(20, 42),
            Size = new Size(330, 40),
            Font = new Font("Segoe UI", 9f),
        });

        _countdownLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 84),
            Size = new Size(330, 18),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Silver,
        };
        Controls.Add(_countdownLabel);

        var runButton = new Button
        {
            Text = "Run now",
            Size = new Size(110, 30),
            Location = new Point(238, 108),
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.Black,
            TabStop = false,
        };
        runButton.FlatAppearance.BorderSize = 0;
        runButton.Click += (_, _) => Decide(PromptResult.RunNow);
        Controls.Add(runButton);

        bool allowSnooze = !forced || config.AllowSnoozePastDeadline;
        if (allowSnooze)
        {
            var snoozeButton = new Button
            {
                Text = $"Snooze {config.SnoozeMinutes}m",
                Size = new Size(110, 30),
                Location = new Point(120, 108),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 66),
                ForeColor = Color.Gainsboro,
                TabStop = false,
            };
            snoozeButton.FlatAppearance.BorderSize = 0;
            snoozeButton.Click += (_, _) => Decide(PromptResult.Snooze);
            Controls.Add(snoozeButton);
        }

        _secondsLeft = forced
            ? Math.Max(0, config.ForcedCountdownSeconds)
            : Math.Max(0, config.PromptAutoSnoozeSeconds);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;

        UpdateCountdownText();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Position before the first paint, once the window has its real (DPI-scaled) size.
        PositionBottomRight();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Re-anchor in case the final size settled after load, so it is never clipped.
        PositionBottomRight();
        if (_secondsLeft > 0 || _forced)
            _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        if (_secondsLeft <= 0)
        {
            // Forced -> run automatically; normal -> auto-snooze so it does not linger on screen.
            Decide(_forced ? PromptResult.RunNow : PromptResult.Snooze);
            return;
        }
        UpdateCountdownText();
    }

    private void UpdateCountdownText()
    {
        _countdownLabel.Text = _forced
            ? $"Running automatically in {_secondsLeft}s…  (Esc cancels during the blackout)"
            : $"Auto-snoozes in {_secondsLeft}s if ignored.";
    }

    private void Decide(PromptResult result)
    {
        if (Result != PromptResult.None) return;
        Result = result;
        _timer.Stop();
        Decided?.Invoke(this, result);
        Close();
    }

    private void PositionBottomRight()
    {
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        const int margin = 16;

        // Anchor to the bottom-right, then clamp so the entire window stays inside the
        // work area regardless of DPI scaling or screen size.
        int x = Math.Clamp(wa.Right - Width - margin, wa.Left, Math.Max(wa.Left, wa.Right - Width));
        int y = Math.Clamp(wa.Bottom - Height - margin, wa.Top, Math.Max(wa.Top, wa.Bottom - Height));
        Location = new Point(x, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
