using System;
using System.IO;

namespace OledRefresher;

/// <summary>
/// Central location for the per-user data files (settings + log), kept under
/// %APPDATA%\OledRefresher so they survive app updates and live with the user profile.
/// </summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OledRefresher");

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");

    public static string LogFile => Path.Combine(DataDirectory, "log.txt");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
