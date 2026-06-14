using System;
using System.Runtime.InteropServices;

namespace OledRefresher;

/// <summary>
/// Reports how long it has been since the last keyboard/mouse input system-wide,
/// using Win32 GetLastInputInfo. The unsigned tick arithmetic handles the ~49.7-day wrap.
/// </summary>
internal static class IdleDetector
{
    public static TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        uint idleMs = unchecked(NativeMethods.GetTickCount() - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
