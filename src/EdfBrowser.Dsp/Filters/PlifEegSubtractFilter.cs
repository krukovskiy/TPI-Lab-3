namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's plif_eeg_subtract_filter.c.
/// Powerline Interference (PLI) subtraction filter optimized for EEG signals.
///
/// Similar to <see cref="PlifEcgSubtractFilter"/> but uses a different adaptation rate
/// and more aggressive averaging suited for the lower-amplitude EEG signals.
/// EEG signals require a faster adaptation and tighter PLI tracking because
/// the interference-to-signal ratio is typically much higher than in ECG.
/// </summary>
public sealed class PlifEegSubtractFilter : IFilter
{
    private const int SlotCount = 40;

    // EEG uses faster adaptation: exponential moving average with higher alpha
    private const double AdaptationAlpha = 0.05;

    private readonly double _powerlineFreq;
    private readonly double _sampleRate;
    private readonly int _cycleLength;

    // Averaged interference pattern (one full cycle)
    private readonly double[] _avgCycle;

    // Ring of cycle slots
    private readonly double[][] _cycleSlots;
    private int _currentSlot;

    // Position within current cycle
    private int _cyclePos;

    // Number of cycles accumulated
    private int _cyclesAccumulated;

    // Running exponential average for faster tracking
    private readonly double[] _expAvgCycle;

    private long _sampleCount;

    // Saved state
    private double[] _avgCycleSaved;
    private double[][] _cycleSlotsSaved;
    private int _currentSlotSaved;
    private int _cyclePosSaved;
    private int _cyclesAccumulatedSaved;
    private double[] _expAvgCycleSaved;
    private long _sampleCountSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a PLI subtraction filter for EEG.
    /// </summary>
    /// <param name="powerlineFreq">Powerline frequency (50 or 60 Hz).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public PlifEegSubtractFilter(double powerlineFreq, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(powerlineFreq, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        _powerlineFreq = powerlineFreq;
        _sampleRate = sampleRate;
        _cycleLength = Math.Max(1, (int)Math.Round(sampleRate / powerlineFreq));

        _avgCycle = new double[_cycleLength];
        _expAvgCycle = new double[_cycleLength];

        _cycleSlots = new double[SlotCount][];
        for (int i = 0; i < SlotCount; i++)
        {
            _cycleSlots[i] = new double[_cycleLength];
        }

        _currentSlot = 0;
        _cyclePos = 0;
        _cyclesAccumulated = 0;
        _sampleCount = 0;

        _avgCycleSaved = new double[_cycleLength];
        _expAvgCycleSaved = new double[_cycleLength];
        _cycleSlotsSaved = new double[SlotCount][];
        for (int i = 0; i < SlotCount; i++)
        {
            _cycleSlotsSaved[i] = new double[_cycleLength];
        }
    }

    /// <inheritdoc />
    public string Label => $"PLI-EEG {_powerlineFreq:F0}Hz fs={_sampleRate:F0}Hz";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        _sampleCount++;

        // Store into current cycle slot
        _cycleSlots[_currentSlot][_cyclePos] = input;

        // Update the exponential moving average at this cycle position
        // This provides faster tracking for EEG
        _expAvgCycle[_cyclePos] = AdaptationAlpha * input + (1.0 - AdaptationAlpha) * _expAvgCycle[_cyclePos];

        double output;

        if (_cyclesAccumulated >= 2)
        {
            // Blend exponential average with slot average for robust estimation
            double blendedEstimate = 0.5 * _avgCycle[_cyclePos] + 0.5 * _expAvgCycle[_cyclePos];
            output = input - blendedEstimate;
        }
        else if (_cyclesAccumulated >= 1)
        {
            // Use only exponential average during early learning
            output = input - _expAvgCycle[_cyclePos];
        }
        else
        {
            output = input;
        }

        _cyclePos++;

        if (_cyclePos >= _cycleLength)
        {
            _cyclePos = 0;

            if (_cyclesAccumulated < SlotCount)
            {
                _cyclesAccumulated++;
            }

            _currentSlot = (_currentSlot + 1) % SlotCount;

            RecomputeAverageCycle();
        }

        return output;
    }

    private void RecomputeAverageCycle()
    {
        int slotsToAverage = Math.Min(_cyclesAccumulated, SlotCount);

        if (slotsToAverage == 0)
        {
            return;
        }

        for (int pos = 0; pos < _cycleLength; pos++)
        {
            double sum = 0.0;

            for (int s = 0; s < slotsToAverage; s++)
            {
                int slotIdx = (_currentSlot + SlotCount - slotsToAverage + s) % SlotCount;
                sum += _cycleSlots[slotIdx][pos];
            }

            _avgCycle[pos] = sum / slotsToAverage;
        }
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        Array.Fill(_avgCycle, 0.0);
        Array.Fill(_expAvgCycle, 0.0);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Fill(_cycleSlots[i], 0.0);
        }

        _currentSlot = 0;
        _cyclePos = 0;
        _cyclesAccumulated = 0;
        _sampleCount = 0;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        Array.Copy(_avgCycle, _avgCycleSaved, _cycleLength);
        Array.Copy(_expAvgCycle, _expAvgCycleSaved, _cycleLength);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Copy(_cycleSlots[i], _cycleSlotsSaved[i], _cycleLength);
        }

        _currentSlotSaved = _currentSlot;
        _cyclePosSaved = _cyclePos;
        _cyclesAccumulatedSaved = _cyclesAccumulated;
        _sampleCountSaved = _sampleCount;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        Array.Copy(_avgCycleSaved, _avgCycle, _cycleLength);
        Array.Copy(_expAvgCycleSaved, _expAvgCycle, _cycleLength);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Copy(_cycleSlotsSaved[i], _cycleSlots[i], _cycleLength);
        }

        _currentSlot = _currentSlotSaved;
        _cyclePos = _cyclePosSaved;
        _cyclesAccumulated = _cyclesAccumulatedSaved;
        _sampleCount = _sampleCountSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new PlifEegSubtractFilter(_powerlineFreq, _sampleRate);

        Array.Copy(_avgCycle, copy._avgCycle, _cycleLength);
        Array.Copy(_expAvgCycle, copy._expAvgCycle, _cycleLength);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Copy(_cycleSlots[i], copy._cycleSlots[i], _cycleLength);
        }

        copy._currentSlot = _currentSlot;
        copy._cyclePos = _cyclePos;
        copy._cyclesAccumulated = _cyclesAccumulated;
        copy._sampleCount = _sampleCount;

        Array.Copy(_avgCycleSaved, copy._avgCycleSaved, _cycleLength);
        Array.Copy(_expAvgCycleSaved, copy._expAvgCycleSaved, _cycleLength);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Copy(_cycleSlotsSaved[i], copy._cycleSlotsSaved[i], _cycleLength);
        }

        copy._currentSlotSaved = _currentSlotSaved;
        copy._cyclePosSaved = _cyclePosSaved;
        copy._cyclesAccumulatedSaved = _cyclesAccumulatedSaved;
        copy._sampleCountSaved = _sampleCountSaved;

        return copy;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
