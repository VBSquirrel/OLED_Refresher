using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OledRefresher;

/// <summary>
/// Runs a single blackout: optionally minimizes a full-screen foreground app, covers the
/// target display(s) in black for the configured duration, supports early dismissal on input,
/// then cleans up and restores the previous foreground window. Non-blocking; everything runs
/// on the UI thread driven by WinForms timers.
/// </summary>
internal sealed class BlackoutController
{
    private readonly AppConfig _config;
    private readonly List<BlackoutForm> _forms = new();

    private System.Windows.Forms.Timer? _durationTimer;
    private System.Windows.Forms.Timer? _graceTimer;
    private Action? _onCompleted;
    private bool _inputArmed;
    private bool _finishing;
    private Point _armReference;
    private IntPtr _minimizedWindow = IntPtr.Zero;
    private bool _cursorHidden;

    public bool IsRunning { get; private set; }

    public BlackoutController(AppConfig config) => _config = config;

    public void Run(Action? onCompleted)
    {
        if (IsRunning)
        {
            onCompleted?.Invoke();
            return;
        }

        IsRunning = true;
        _finishing = false;
        _inputArmed = false;
        _onCompleted = onCompleted;

        try
        {
            MaybeMinimizeForegroundApp();

            var screens = _config.BlackoutAllMonitors
                ? Screen.AllScreens
                : new[] { Screen.PrimaryScreen ?? Screen.AllScreens[0] };

            foreach (var screen in screens)
            {
                var form = new BlackoutForm(screen.Bounds);
                form.KeyDown += OnKeyDown;
                form.MouseDown += OnMouseInput;
                form.MouseMove += OnMouseMove;
                _forms.Add(form);
            }

            Cursor.Hide();
            _cursorHidden = true;

            foreach (var form in _forms)
                form.Show();

            // Give one overlay focus so it captures the keyboard and pulls the desktop
            // out of an exclusive-fullscreen game.
            if (_forms.Count > 0)
            {
                _forms[0].Activate();
                _forms[0].Focus();
            }

            _armReference = Cursor.Position;

            _graceTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, _config.InputGraceMilliseconds) };
            _graceTimer.Tick += (_, _) =>
            {
                _inputArmed = true;
                _armReference = Cursor.Position;
                _graceTimer!.Stop();
            };
            _graceTimer.Start();

            _durationTimer = new System.Windows.Forms.Timer { Interval = Math.Max(200, _config.OverlaySeconds * 1000) };
            _durationTimer.Tick += (_, _) => Finish();
            _durationTimer.Start();

            Logger.Info($"Blackout started on {_forms.Count} display(s).");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start blackout", ex);
            Finish();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Finish(); // panic key always cancels, even when DismissOnInput is off
            return;
        }

        if (_config.DismissOnInput && _inputArmed)
            Finish();
    }

    private void OnMouseInput(object? sender, MouseEventArgs e)
    {
        if (_config.DismissOnInput && _inputArmed)
            Finish();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_config.DismissOnInput || !_inputArmed)
            return;

        var p = Cursor.Position;
        int dx = p.X - _armReference.X;
        int dy = p.Y - _armReference.Y;
        if ((dx * dx) + (dy * dy) > 64) // moved more than ~8px
            Finish();
    }

    private void Finish()
    {
        if (_finishing) return;
        _finishing = true;

        _durationTimer?.Stop();
        _durationTimer?.Dispose();
        _durationTimer = null;

        _graceTimer?.Stop();
        _graceTimer?.Dispose();
        _graceTimer = null;

        foreach (var form in _forms)
        {
            try
            {
                form.Close();
                form.Dispose();
            }
            catch
            {
                // ignore individual form teardown failures
            }
        }
        _forms.Clear();

        if (_cursorHidden)
        {
            Cursor.Show();
            _cursorHidden = false;
        }

        RestoreMinimizedApp();

        IsRunning = false;
        Logger.Info("Blackout finished.");

        var callback = _onCompleted;
        _onCompleted = null;
        callback?.Invoke();
    }

    private void MaybeMinimizeForegroundApp()
    {
        _minimizedWindow = IntPtr.Zero;
        if (!_config.UseMinimizeFallback)
            return;

        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || NativeMethods.IsIconic(hwnd))
                return;
            if (!NativeMethods.GetWindowRect(hwnd, out var r))
                return;

            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            // Only minimize when the window covers an entire screen — i.e. it is most likely a
            // full-screen game that a plain overlay might not be able to draw over.
            foreach (var screen in Screen.AllScreens)
            {
                var b = screen.Bounds;
                if (w >= b.Width && h >= b.Height)
                {
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
                    _minimizedWindow = hwnd;
                    Logger.Info("Minimized full-screen foreground window (fallback).");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Minimize fallback failed", ex);
        }
    }

    private void RestoreMinimizedApp()
    {
        if (_minimizedWindow == IntPtr.Zero)
            return;

        try
        {
            NativeMethods.ShowWindow(_minimizedWindow, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(_minimizedWindow);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to restore minimized window", ex);
        }
        finally
        {
            _minimizedWindow = IntPtr.Zero;
        }
    }
}
