namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's spike_filter.c.
/// Detects and removes spikes from signals using velocity-based detection
/// and linear interpolation replacement.
/// </summary>
public sealed class SpikeFilter : IFilter
{
    private const int BufSize = 3;

    private readonly double _velocityThreshold;
    private readonly int _holdoff;
    private readonly double _sampleRate;

    // Circular buffers of size 3
    private readonly double[] _inputBuf;
    private readonly double[] _velocityBuf;
    private int _bufIdx;
    private int _holdoffCounter;
    private double _lastCleanValue;

    // Saved state
    private readonly double[] _inputBufSaved;
    private readonly double[] _velocityBufSaved;
    private int _bufIdxSaved;
    private int _holdoffCounterSaved;
    private double _lastCleanValueSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a spike filter.
    /// </summary>
    /// <param name="velocity">Velocity threshold for spike detection (units per sample).</param>
    /// <param name="holdoff">Number of samples to hold off after spike detection.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public SpikeFilter(double velocity, int holdoff, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(velocity, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(holdoff, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        _velocityThreshold = velocity;
        _holdoff = holdoff;
        _sampleRate = sampleRate;

        _inputBuf = new double[BufSize];
        _velocityBuf = new double[BufSize];
        _inputBufSaved = new double[BufSize];
        _velocityBufSaved = new double[BufSize];
        _bufIdx = 0;
        _holdoffCounter = 0;
        _lastCleanValue = 0.0;
    }

    /// <inheritdoc />
    public string Label => $"Spike vel={_velocityThreshold:G4} holdoff={_holdoff}";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        // Compute velocity (difference from previous input)
        int prevIdx = (_bufIdx + BufSize - 1) % BufSize;
        double velocity = input - _inputBuf[prevIdx];

        _inputBuf[_bufIdx] = input;
        _velocityBuf[_bufIdx] = velocity;

        double output;

        if (_holdoffCounter > 0)
        {
            // During holdoff, output the last known clean value
            output = _lastCleanValue;
            _holdoffCounter--;
        }
        else if (Math.Abs(velocity) > _velocityThreshold)
        {
            // Spike detected: use last clean value and start holdoff
            output = _lastCleanValue;
            _holdoffCounter = _holdoff;
        }
        else
        {
            // Normal sample
            output = input;
            _lastCleanValue = input;
        }

        _bufIdx = (_bufIdx + 1) % BufSize;

        return output;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        Array.Fill(_inputBuf, initialValue);
        Array.Fill(_velocityBuf, 0.0);
        _bufIdx = 0;
        _holdoffCounter = 0;
        _lastCleanValue = initialValue;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        Array.Copy(_inputBuf, _inputBufSaved, BufSize);
        Array.Copy(_velocityBuf, _velocityBufSaved, BufSize);
        _bufIdxSaved = _bufIdx;
        _holdoffCounterSaved = _holdoffCounter;
        _lastCleanValueSaved = _lastCleanValue;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        Array.Copy(_inputBufSaved, _inputBuf, BufSize);
        Array.Copy(_velocityBufSaved, _velocityBuf, BufSize);
        _bufIdx = _bufIdxSaved;
        _holdoffCounter = _holdoffCounterSaved;
        _lastCleanValue = _lastCleanValueSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new SpikeFilter(_velocityThreshold, _holdoff, _sampleRate);
        Array.Copy(_inputBuf, copy._inputBuf, BufSize);
        Array.Copy(_velocityBuf, copy._velocityBuf, BufSize);
        copy._bufIdx = _bufIdx;
        copy._holdoffCounter = _holdoffCounter;
        copy._lastCleanValue = _lastCleanValue;
        Array.Copy(_inputBufSaved, copy._inputBufSaved, BufSize);
        Array.Copy(_velocityBufSaved, copy._velocityBufSaved, BufSize);
        copy._bufIdxSaved = _bufIdxSaved;
        copy._holdoffCounterSaved = _holdoffCounterSaved;
        copy._lastCleanValueSaved = _lastCleanValueSaved;
        return copy;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
