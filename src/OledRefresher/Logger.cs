using System;
using System.IO;

namespace OledRefresher;

/// <summary>
/// Tiny append-only logger with a size cap. Logging must never throw, so every
/// failure is swallowed — a diagnostics helper should not be able to crash the app.
/// </summary>
internal static class Logger
{
    private static readonly object Gate = new();
    private const long MaxBytes = 512 * 1024;

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                AppPaths.EnsureDataDirectory();
                var path = AppPaths.LogFile;

                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    var rolled = path + ".1";
                    File.Delete(rolled); // no-op if it does not exist
                    File.Move(path, rolled);
                }

                File.AppendAllText(
                    path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Never let logging take down the application.
        }
    }
}
