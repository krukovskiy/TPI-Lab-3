using CommunityToolkit.Mvvm.ComponentModel;

namespace EdfBrowser.App.Models;

/// <summary>
/// Represents the state of a crosshair cursor on the signal viewer.
/// </summary>
public sealed partial class CrosshairState : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private double _xPixel;

    [ObservableProperty]
    private double _yPixel;

    [ObservableProperty]
    private long _timeTicks;

    [ObservableProperty]
    private double _value;

    /// <summary>
    /// Formatted time string for display.
    /// </summary>
    public string TimeText => TimeSpan.FromTicks(_timeTicks).ToString(@"hh\:mm\:ss\.fff");

    partial void OnTimeTicksChanged(long value)
    {
        OnPropertyChanged(nameof(TimeText));
    }

    /// <summary>
    /// Resets the crosshair to inactive state.
    /// </summary>
    public void Reset()
    {
        IsActive = false;
        XPixel = 0;
        YPixel = 0;
        TimeTicks = 0;
        Value = 0;
    }
}
