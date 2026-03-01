using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EdfBrowser.App.ViewModels;

/// <summary>
/// ViewModel for a single displayed signal trace.
/// Each instance represents one signal channel shown in the waveform viewer.
/// </summary>
public sealed partial class SignalTraceViewModel : ObservableObject
{
    /// <summary>Index of the file this signal belongs to.</summary>
    [ObservableProperty]
    private int _fileIndex;

    /// <summary>Signal index within the file (user signal index, annotation channels skipped).</summary>
    [ObservableProperty]
    private int _signalIndex;

    /// <summary>Display label for the signal.</summary>
    [ObservableProperty]
    private string _label = "";

    /// <summary>Vertical sensitivity multiplier.</summary>
    [ObservableProperty]
    private double _sensitivity = 1.0;

    /// <summary>Vertical screen offset in pixels from center.</summary>
    [ObservableProperty]
    private int _screenOffset;

    /// <summary>Signal polarity (+1 or -1).</summary>
    [ObservableProperty]
    private int _polarity = 1;

    /// <summary>Color used to draw the trace.</summary>
    [ObservableProperty]
    private Color _traceColor;

    /// <summary>Whether this trace is currently visible.</summary>
    [ObservableProperty]
    private bool _visible = true;

    /// <summary>Physical dimension / units (e.g. "uV").</summary>
    [ObservableProperty]
    private string _physicalDimension = "";

    /// <summary>Sample frequency in Hz.</summary>
    [ObservableProperty]
    private double _sampleFrequency;

    // --- Display info ---

    /// <summary>Current sample value at the crosshair position.</summary>
    [ObservableProperty]
    private double _currentValue;

    /// <summary>Minimum value in the current view window.</summary>
    [ObservableProperty]
    private double _minValue;

    /// <summary>Maximum value in the current view window.</summary>
    [ObservableProperty]
    private double _maxValue;

    /// <summary>Doubles the vertical sensitivity (zoom in on amplitude).</summary>
    [RelayCommand]
    private void IncreaseAmplitude()
    {
        Sensitivity *= 2;
    }

    /// <summary>Halves the vertical sensitivity (zoom out on amplitude).</summary>
    [RelayCommand]
    private void DecreaseAmplitude()
    {
        Sensitivity /= 2;
    }

    /// <summary>Inverts the signal polarity.</summary>
    [RelayCommand]
    private void InvertSignal()
    {
        Polarity *= -1;
    }

    /// <summary>Toggles visibility of this trace.</summary>
    [RelayCommand]
    private void ToggleVisibility()
    {
        Visible = !Visible;
    }
}
