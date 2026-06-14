using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OledRefresher;

/// <summary>
/// Manages the per-user "run at sign-in" entry via the HKCU Run key. This needs no admin
/// rights and is the standard, OS-native way to auto-start a tray utility at login.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OledRefresher";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return;

            if (enabled)
                key.SetValue(ValueName, $"\"{ExecutablePath}\"");
            else if (key.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not update 'Start with Windows' setting", ex);
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string ExecutablePath => Environment.ProcessPath ?? Application.ExecutablePath;
}
