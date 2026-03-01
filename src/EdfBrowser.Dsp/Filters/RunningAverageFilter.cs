namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's ravg_filter.c.
/// Running average filter supporting simple (type 0) and weighted/triangular (type 1) modes.
/// </summary>
public sealed class RunningAverageFilter : IFilter
{
    private readonly int _size;
    private readonly int _type;
    private readonly double[] _buffer;
    private int _bufPos;
    private double _runningSum;
    private int _sampleCount;

    // Saved state
    private readonly double[] _bufferSaved;
    private int _bufPosSaved;
    private double _runningSumSaved;
    private int _sampleCountSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a running average filter.
    /// </summary>
    /// <param name="size">Window size (number of samples).</param>
    /// <param name="type">0 = simple running mean, 1 = weighted/triangular running mean.</param>
    public RunningAverageFilter(int size, int type)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        if (type is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Type must be 0 (simple) or 1 (weighted).");
        }

        _size = size;
        _type = type;
        _buffer = new double[size];
        _bufferSaved = new double[size];
        _bufPos = 0;
        _runningSum = 0.0;
        _sampleCount = 0;
    }

    /// <inheritdoc />
    public string Label => _type == 0
        ? $"RunAvg simple N={_size}"
        : $"RunAvg weighted N={_size}";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        if (_type == 0)
        {
            return ProcessSimple(input);
        }
        else
        {
            return ProcessWeighted(input);
        }
    }

    private double ProcessSimple(double input)
    {
        // Subtract the oldest value and add the new one
        _runningSum -= _buffer[_bufPos];
        _buffer[_bufPos] = input;
        _runningSum += input;

        _bufPos++;
        if (_bufPos >= _size)
        {
            _bufPos = 0;
        }

        if (_sampleCount < _size)
        {
            _sampleCount++;
        }

        return _runningSum / _sampleCount;
    }

    private double ProcessWeighted(double input)
    {
        // Store new sample in circular buffer
        _buffer[_bufPos] = input;

        _bufPos++;
        if (_bufPos >= _size)
        {
            _bufPos = 0;
        }

        if (_sampleCount < _size)
        {
            _sampleCount++;
        }

        // Compute triangular-weighted average
        // Weights: 1, 2, 3, ..., N/2+1, ..., 3, 2, 1 (triangular window)
        double weightedSum = 0.0;
        double weightTotal = 0.0;
        int halfSize = _sampleCount / 2;

        for (int i = 0; i < _sampleCount; i++)
        {
            int idx = (_bufPos + _size - _sampleCount + i) % _size;

            // Triangular weight: rises to center, then falls
            int weight = i <= halfSize ? (i + 1) : (_sampleCount - i);
            weightedSum += _buffer[idx] * weight;
            weightTotal += weight;
        }

        return weightTotal > 0.0 ? weightedSum / weightTotal : 0.0;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        Array.Fill(_buffer, initialValue);
        _bufPos = 0;
        _runningSum = initialValue * _size;
        _sampleCount = 0;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        Array.Copy(_buffer, _bufferSaved, _size);
        _bufPosSaved = _bufPos;
        _runningSumSaved = _runningSum;
        _sampleCountSaved = _sampleCount;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        Array.Copy(_bufferSaved, _buffer, _size);
        _bufPos = _bufPosSaved;
        _runningSum = _runningSumSaved;
        _sampleCount = _sampleCountSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new RunningAverageFilter(_size, _type);
        Array.Copy(_buffer, copy._buffer, _size);
        copy._bufPos = _bufPos;
        copy._runningSum = _runningSum;
        copy._sampleCount = _sampleCount;
        Array.Copy(_bufferSaved, copy._bufferSaved, _size);
        copy._bufPosSaved = _bufPosSaved;
        copy._runningSumSaved = _runningSumSaved;
        copy._sampleCountSaved = _sampleCountSaved;
        return copy;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
