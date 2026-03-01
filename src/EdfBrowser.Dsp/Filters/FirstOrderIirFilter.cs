namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Filter type enumeration for first-order IIR filters.
/// </summary>
public enum FilterType
{
    LowPass,
    HighPass
}

/// <summary>
/// Port of EDFbrowser's filter.c (filtset_t).
/// Single-pole first-order IIR filter for lowpass and highpass operation.
/// </summary>
public sealed class FirstOrderIirFilter : IFilter, IFrequencyResponse
{
    private readonly FilterType _type;
    private readonly double _cutoffHz;
    private readonly double _sampleRate;
    private readonly double _factor;

    private double _oldInput;
    private double _oldOutput;

    // Saved state
    private double _oldInputSaved;
    private double _oldOutputSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a first-order IIR filter.
    /// </summary>
    /// <param name="type">LowPass or HighPass.</param>
    /// <param name="cutoffHz">Cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public FirstOrderIirFilter(FilterType type, double cutoffHz, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cutoffHz, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        _type = type;
        _cutoffHz = cutoffHz;
        _sampleRate = sampleRate;

        double sampleInterval = 1.0 / sampleRate;
        double tau = 1.0 / (2.0 * Math.PI * cutoffHz);

        _factor = type == FilterType.LowPass
            ? sampleInterval / (sampleInterval + tau)
            : tau / (sampleInterval + tau);
    }

    /// <inheritdoc />
    public string Label => $"{_type} IIR fc={_cutoffHz:F2}Hz fs={_sampleRate:F0}Hz";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        double output;

        if (_type == FilterType.LowPass)
        {
            output = _oldOutput + _factor * (input - _oldOutput);
        }
        else
        {
            output = _factor * (_oldOutput + input - _oldInput);
        }

        _oldInput = input;
        _oldOutput = output;

        return output;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        _oldInput = initialValue;
        _oldOutput = initialValue;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        _oldInputSaved = _oldInput;
        _oldOutputSaved = _oldOutput;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        _oldInput = _oldInputSaved;
        _oldOutput = _oldOutputSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new FirstOrderIirFilter(_type, _cutoffHz, _sampleRate);
        copy._oldInput = _oldInput;
        copy._oldOutput = _oldOutput;
        copy._oldInputSaved = _oldInputSaved;
        copy._oldOutputSaved = _oldOutputSaved;
        return copy;
    }

    /// <inheritdoc />
    public double GetMagnitudeResponse(double frequencyHz, double sampleRate)
    {
        // For a first-order filter, magnitude response:
        // H(f) = factor / sqrt(1 + factor^2 - 2*factor*cos(2*pi*f/fs)) approximately
        // More precisely, compute from the transfer function.
        double omega = 2.0 * Math.PI * frequencyHz / sampleRate;

        if (_type == FilterType.LowPass)
        {
            // H(z) = factor / (1 - (1-factor)*z^-1)
            double cosW = Math.Cos(omega);
            double sinW = Math.Sin(omega);
            double a1 = 1.0 - _factor;
            double denomReal = 1.0 - a1 * cosW;
            double denomImag = a1 * sinW;
            double denomMag = Math.Sqrt(denomReal * denomReal + denomImag * denomImag);
            return _factor / denomMag;
        }
        else
        {
            // HPF: H(z) = factor * (1 - z^-1) / (1 - factor*z^-1)
            double cosW = Math.Cos(omega);
            double sinW = Math.Sin(omega);

            double numReal = 1.0 - cosW;
            double numImag = sinW;
            double numMag = Math.Sqrt(numReal * numReal + numImag * numImag);

            double denomReal = 1.0 - _factor * cosW;
            double denomImag = _factor * sinW;
            double denomMag = Math.Sqrt(denomReal * denomReal + denomImag * denomImag);

            return _factor * numMag / denomMag;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
