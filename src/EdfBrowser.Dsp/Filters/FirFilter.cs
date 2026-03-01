namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's fir_filter.c.
/// Finite Impulse Response filter using a circular buffer.
/// </summary>
public sealed class FirFilter : IFilter, IFrequencyResponse
{
    private readonly int _taps;
    private readonly double[] _coefficients;
    private readonly double[] _buffer;
    private int _bufferIndex;

    // Saved state
    private readonly double[] _bufferSaved;
    private int _bufferIndexSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a FIR filter with the given coefficients.
    /// </summary>
    /// <param name="coefficients">Filter coefficients (tap weights). The length determines the filter order.</param>
    public FirFilter(double[] coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        ArgumentOutOfRangeException.ThrowIfZero(coefficients.Length);

        _taps = coefficients.Length;
        _coefficients = new double[_taps];
        Array.Copy(coefficients, _coefficients, _taps);

        _buffer = new double[_taps];
        _bufferSaved = new double[_taps];
        _bufferIndex = 0;
        _bufferIndexSaved = 0;
    }

    /// <inheritdoc />
    public string Label => $"FIR taps={_taps}";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        // Store input in circular buffer at current position
        _buffer[_bufferIndex] = input;

        // Compute convolution sum
        double output = 0.0;
        int idx = _bufferIndex;

        for (int i = 0; i < _taps; i++)
        {
            output += _buffer[idx] * _coefficients[i];
            idx--;
            if (idx < 0)
            {
                idx = _taps - 1;
            }
        }

        // Advance buffer index
        _bufferIndex++;
        if (_bufferIndex >= _taps)
        {
            _bufferIndex = 0;
        }

        return output;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        Array.Fill(_buffer, initialValue);
        _bufferIndex = 0;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        Array.Copy(_buffer, _bufferSaved, _taps);
        _bufferIndexSaved = _bufferIndex;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        Array.Copy(_bufferSaved, _buffer, _taps);
        _bufferIndex = _bufferIndexSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new FirFilter(_coefficients);
        Array.Copy(_buffer, copy._buffer, _taps);
        copy._bufferIndex = _bufferIndex;
        Array.Copy(_bufferSaved, copy._bufferSaved, _taps);
        copy._bufferIndexSaved = _bufferIndexSaved;
        return copy;
    }

    /// <inheritdoc />
    public double GetMagnitudeResponse(double frequencyHz, double sampleRate)
    {
        double omega = 2.0 * Math.PI * frequencyHz / sampleRate;
        double realPart = 0.0;
        double imagPart = 0.0;

        for (int i = 0; i < _taps; i++)
        {
            realPart += _coefficients[i] * Math.Cos(omega * i);
            imagPart -= _coefficients[i] * Math.Sin(omega * i);
        }

        return Math.Sqrt(realPart * realPart + imagPart * imagPart);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
