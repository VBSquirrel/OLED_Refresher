using System;
using System.Windows.Forms;

namespace OledRefresher;

/// <summary>
/// A hidden message window that receives a single global hotkey (default Ctrl+Alt+B)
/// for an on-demand "refresh now". Registration failure is non-fatal.
/// </summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0xB0B;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public bool TryRegister(uint modifiers, uint virtualKey)
    {
        try
        {
            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers, virtualKey);
            if (!_registered)
                Logger.Warn("Global hotkey registration failed (it may already be in use).");
            return _registered;
        }
        catch (Exception ex)
        {
            Logger.Error("Hotkey registration error", ex);
            return false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        try
        {
            if (_registered)
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        }
        catch
        {
            // ignore
        }

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}
