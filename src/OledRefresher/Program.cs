using System;
using System.Threading;
using System.Windows.Forms;

namespace OledRefresher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Single instance: a second launch (e.g. from the Run key while already running) exits quietly.
        using var mutex = new Mutex(initiallyOwned: true, name: @"Local\OledRefresher_SingleInstance", out bool createdNew);
        if (!createdNew)
            return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Logger.Error("UI thread exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Error("Unhandled exception", e.ExceptionObject as Exception);

        AppConfig config;
        try
        {
            config = AppConfig.Load();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load config; using defaults", ex);
            config = new AppConfig();
        }

        StartupManager.Apply(config.StartWithWindows);

        Logger.Info("OLED Refresher starting.");
        try
        {
            Application.Run(new TrayApplicationContext(config));
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error in message loop", ex);
        }

        Logger.Info("OLED Refresher exiting.");
        GC.KeepAlive(mutex);
    }
}
