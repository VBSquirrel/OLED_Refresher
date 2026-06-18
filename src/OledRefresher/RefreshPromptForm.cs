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
///
/// The window auto-sizes to its content (labels wrap, buttons grow to their text) so nothing
/// is ever clipped at any DPI scale. It is a non-activating top-most tool window so it does not
/// steal focus (and therefore input) from the game underneath.
/// </summary>
internal sealed class RefreshPromptForm : Form
{
    // Logical width the text wraps at; the form grows to fit (and DPI-scales with everything else).
    private const int ContentWidth = 300;

    private readonly bool _forced;
    private readonly Color _accent;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _countdownLabel;
    private int _secondsLeft;

    public PromptResult Result { get; private set; } = PromptResult.None;
    public event EventHandler<PromptResult>? Decided;

    public RefreshPromptForm(AppConfig config, bool forced, TimeSpan sinceLastRefresh)
    {
        _forced = forced;
        _accent = forced ? Color.FromArgb(220, 80, 60) : Color.FromArgb(46, 196, 182);

        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.Gainsboro;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(300, 0);
        // Left padding leaves room for the painted accent strip.
        Padding = new Padding(18, 14, 16, 14);
        ResizeRedraw = true; // repaint the accent strip when auto-size changes the height

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = forced ? "OLED protection — refresh required" : "OLED refresh due",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = forced ? Color.FromArgb(255, 140, 120) : Color.White,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Margin = new Padding(0, 0, 0, 6),
        };

        int mins = Math.Max(1, (int)Math.Round(sinceLastRefresh.TotalMinutes));
        var messageLabel = new Label
        {
            Text = forced
                ? $"Your panel hasn't been rested for ~{mins} min. A {config.OverlaySeconds}-second black refresh will run automatically."
                : "Time to rest your panel with a brief black screen.",
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0), // wrap at this width, grow in height
            Margin = new Padding(0, 0, 0, 6),
        };

        _countdownLabel = new Label
        {
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Silver,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Margin = new Padding(0, 0, 0, 10),
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
        };

        var runButton = MakeButton("Run now", _accent, Color.Black);
        runButton.Click += (_, _) => Decide(PromptResult.RunNow);
        buttons.Controls.Add(runButton); // rightmost (RightToLeft flow)

        bool allowSnooze = !forced || config.AllowSnoozePastDeadline;
        if (allowSnooze)
        {
            var snoozeButton = MakeButton($"Snooze {config.SnoozeMinutes}m", Color.FromArgb(60, 60, 66), Color.Gainsboro);
            snoozeButton.Margin = new Padding(0, 0, 8, 0);
            snoozeButton.Click += (_, _) => Decide(PromptResult.Snooze);
            buttons.Controls.Add(snoozeButton); // left of "Run now"
        }

        layout.Controls.Add(titleLabel);
        layout.Controls.Add(messageLabel);
        layout.Controls.Add(_countdownLabel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        _secondsLeft = forced
            ? Math.Max(0, config.ForcedCountdownSeconds)
            : Math.Max(0, config.PromptAutoSnoozeSeconds);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;

        UpdateCountdownText();
    }

    private static Button MakeButton(string text, Color back, Color fore)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(96, 30),
            Padding = new Padding(12, 6, 12, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fore,
            TabStop = false,
            Margin = new Padding(0),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Accent strip down the left edge, inside the left padding.
        using var brush = new SolidBrush(_accent);
        e.Graphics.FillRectangle(brush, 0, 0, 6, Height);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Position before the first paint, once the window has its real (auto-sized) size.
        PositionBottomRight();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Re-anchor once the final size has settled so it is never clipped.
        PositionBottomRight();
        if (_secondsLeft > 0 || _forced)
            _timer.Start();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Keep the toast anchored to the corner if auto-size changes its dimensions.
        if (IsHandleCreated && Visible)
            PositionBottomRight();
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
