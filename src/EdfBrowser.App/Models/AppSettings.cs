namespace EdfBrowser.App.Models;

/// <summary>
/// Application-wide settings model.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Whether to use linear interpolation between samples.</summary>
    public bool LinearInterpolation { get; set; } = true;

    /// <summary>Whether to draw baseline indicators for each signal.</summary>
    public bool ShowBaselines { get; set; } = true;

    /// <summary>Whether to show the time grid.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Whether to enable anti-aliasing in the renderer.</summary>
    public bool AntiAliasing { get; set; } = true;

    /// <summary>Default visible page time in seconds.</summary>
    public double DefaultPageTime { get; set; } = 10.0;

    /// <summary>Whether the remote control TCP server is enabled.</summary>
    public bool RemoteControlEnabled { get; set; }

    /// <summary>TCP port for the remote control server.</summary>
    public int RemoteControlPort { get; set; } = 40100;

    /// <summary>Creates a deep copy of these settings.</summary>
    public AppSettings Clone() => new()
    {
        LinearInterpolation = LinearInterpolation,
        ShowBaselines = ShowBaselines,
        ShowGrid = ShowGrid,
        AntiAliasing = AntiAliasing,
        DefaultPageTime = DefaultPageTime,
        RemoteControlEnabled = RemoteControlEnabled,
        RemoteControlPort = RemoteControlPort,
    };
}
