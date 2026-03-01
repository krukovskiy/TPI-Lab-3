using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdfBrowser.Dsp.Fft;

namespace EdfBrowser.App.ViewModels;

/// <summary>
/// ViewModel for the spectrum analysis panel.
/// Manages FFT settings and computed spectrum data.
/// </summary>
public sealed partial class SpectrumViewModel : ObservableObject
{
    /// <summary>FFT size (number of points).</summary>
    [ObservableProperty]
    private int _fftSize = 4096;

    /// <summary>Window function type for the FFT.</summary>
    [ObservableProperty]
    private WindowType _windowType = WindowType.Hanning;

    /// <summary>Whether to use a logarithmic frequency scale.</summary>
    [ObservableProperty]
    private bool _logScale;

    /// <summary>Whether the spectrum panel is visible.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>Index of the signal being analyzed (-1 for none).</summary>
    [ObservableProperty]
    private int _selectedSignalIndex = -1;

    /// <summary>Computed magnitude spectrum data.</summary>
    [ObservableProperty]
    private double[]? _spectrumData;

    /// <summary>Frequency values corresponding to each bin.</summary>
    [ObservableProperty]
    private double[]? _frequencyBins;

    /// <summary>Maximum frequency (Nyquist) for the current signal.</summary>
    [ObservableProperty]
    private double _maxFrequency;

    /// <summary>
    /// Computes the magnitude spectrum from the given samples using the current FFT settings.
    /// </summary>
    /// <param name="samples">Time-domain samples.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public void ComputeSpectrum(double[] samples, double sampleRate)
    {
        if (samples.Length == 0 || sampleRate <= 0) return;

        int n = Math.Min(samples.Length, FftSize);
        MaxFrequency = sampleRate / 2.0;

        // Copy and window the data
        var windowed = new double[n];
        Array.Copy(samples, windowed, n);
        WindowFunction.Apply(windowed.AsSpan(), WindowType);

        // Compute FFT magnitude (real FFT, output is N/2+1 bins)
        int halfN = n / 2 + 1;
        var magnitude = new double[halfN];
        var freqBins = new double[halfN];

        // Simple DFT for magnitude computation
        double freqResolution = sampleRate / n;
        for (int k = 0; k < halfN; k++)
        {
            double real = 0;
            double imag = 0;
            for (int i = 0; i < n; i++)
            {
                double angle = -2.0 * Math.PI * k * i / n;
                real += windowed[i] * Math.Cos(angle);
                imag += windowed[i] * Math.Sin(angle);
            }

            magnitude[k] = Math.Sqrt(real * real + imag * imag) / n;
            freqBins[k] = k * freqResolution;
        }

        // Convert to dB if log scale
        if (LogScale)
        {
            for (int k = 0; k < halfN; k++)
            {
                magnitude[k] = magnitude[k] > 0 ? 20.0 * Math.Log10(magnitude[k]) : -200.0;
            }
        }

        SpectrumData = magnitude;
        FrequencyBins = freqBins;
    }

    /// <summary>Toggles visibility of the spectrum panel.</summary>
    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }
}
