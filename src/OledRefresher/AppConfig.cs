using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OledRefresher;

/// <summary>
/// User-editable settings, persisted as indented JSON at <see cref="AppPaths.ConfigFile"/>.
/// All numeric values are clamped to sane ranges on load.
/// </summary>
internal sealed class AppConfig
{
    /// <summary>How often (minutes) a refresh is attempted.</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>How long (seconds) the screen stays black.</summary>
    public int OverlaySeconds { get; set; } = 2;

    /// <summary>No input for at least this long (seconds) means the user is idle, so the refresh runs silently.</summary>
    public int IdleThresholdSeconds { get; set; } = 60;

    /// <summary>How far (minutes) "Snooze" pushes the next attempt.</summary>
    public int SnoozeMinutes { get; set; } = 30;

    /// <summary>
    /// Backstop: once it has been this long (minutes) since the last refresh, the prompt
    /// escalates to a forced refresh so the panel is rested before burn-in can set in.
    /// </summary>
    public int MaxMinutesSinceRefresh { get; set; } = 60;

    /// <summary>Seconds the forced prompt counts down before it runs automatically.</summary>
    public int ForcedCountdownSeconds { get; set; } = 10;

    /// <summary>If the normal (active) prompt is ignored this long (seconds), it auto-snoozes so it never lingers.</summary>
    public int PromptAutoSnoozeSeconds { get; set; } = 30;

    /// <summary>Whether a forced refresh may still be snoozed. Default false = truly forced.</summary>
    public bool AllowSnoozePastDeadline { get; set; } = false;

    /// <summary>
    /// Minimize a full-screen foreground app before showing the overlay (covers exclusive-fullscreen
    /// games that an overlay cannot draw over), then restore it afterwards.
    /// </summary>
    public bool UseMinimizeFallback { get; set; } = true;

    /// <summary>Any key press or mouse movement dismisses the black overlay early (safety / no disruption).</summary>
    public bool DismissOnInput { get; set; } = true;

    /// <summary>Ignore input for this many ms after the overlay appears, so the launching click does not instantly close it.</summary>
    public int InputGraceMilliseconds { get; set; } = 400;

    /// <summary>Black out every monitor (true) or just the primary display (false).</summary>
    public bool BlackoutAllMonitors { get; set; } = true;

    /// <summary>Launch automatically at Windows sign-in.</summary>
    public bool StartWithWindows { get; set; } = true;

    [JsonIgnore] public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);
    [JsonIgnore] public TimeSpan OverlayDuration => TimeSpan.FromSeconds(OverlaySeconds);
    [JsonIgnore] public TimeSpan IdleThreshold => TimeSpan.FromSeconds(IdleThresholdSeconds);
    [JsonIgnore] public TimeSpan Snooze => TimeSpan.FromMinutes(SnoozeMinutes);
    [JsonIgnore] public TimeSpan MaxSinceRefresh => TimeSpan.FromMinutes(MaxMinutesSinceRefresh);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        AppPaths.EnsureDataDirectory();
        var path = AppPaths.ConfigFile;

        AppConfig config;
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        else
        {
            config = new AppConfig();
        }

        config.Clamp();
        config.Save(); // normalize / ensure every key is present on disk
        return config;
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save config", ex);
        }
    }

    private void Clamp()
    {
        IntervalMinutes = Math.Clamp(IntervalMinutes, 1, 24 * 60);
        OverlaySeconds = Math.Clamp(OverlaySeconds, 1, 30);
        IdleThresholdSeconds = Math.Clamp(IdleThresholdSeconds, 5, 3600);
        SnoozeMinutes = Math.Clamp(SnoozeMinutes, 1, 24 * 60);
        MaxMinutesSinceRefresh = Math.Clamp(MaxMinutesSinceRefresh, IntervalMinutes, 24 * 60);
        ForcedCountdownSeconds = Math.Clamp(ForcedCountdownSeconds, 0, 120);
        PromptAutoSnoozeSeconds = Math.Clamp(PromptAutoSnoozeSeconds, 5, 3600);
        InputGraceMilliseconds = Math.Clamp(InputGraceMilliseconds, 0, 5000);
    }
}
