namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's plif_ecg_subtract_filter.c.
/// Powerline Interference (PLI) subtraction filter for ECG signals.
///
/// This filter learns the powerline interference pattern and subtracts it.
/// It maintains a circular buffer of averaged interference cycles
/// using 40 slots for cycle averaging.
/// </summary>
public sealed class PlifEcgSubtractFilter : IFilter
{
    private const int SlotCount = 40;

    private readonly double _powerlineFreq;
    private readonly double _sampleRate;
    private readonly int _cycleLength;

    // Averaged interference pattern (one full cycle of powerline frequency)
    private readonly double[] _avgCycle;

    // Ring of 40 cycle slots, each holding one full cycle
    private readonly double[][] _cycleSlots;
    private int _currentSlot;

    // Position within current cycle
    private int _cyclePos;

    // Number of cycles accumulated (for averaging)
    private int _cyclesAccumulated;

    // Input sample counter for initial learning phase
    private long _sampleCount;

    // Saved state
    private double[] _avgCycleSaved;
    private double[][] _cycleSlotsSaved;
    private int _currentSlotSaved;
    private int _cyclePosSaved;
    private int _cyclesAccumulatedSaved;
    private long _sampleCountSaved;

    private bool _disposed;

    /// <summary>
    /// Constructs a PLI subtraction filter for ECG.
    /// </summary>
    /// <param name="powerlineFreq">Powerline frequency (50 or 60 Hz).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public PlifEcgSubtractFilter(double powerlineFreq, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(powerlineFreq, 0.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        _powerlineFreq = powerlineFreq;
        _sampleRate = sampleRate;
        _cycleLength = Math.Max(1, (int)Math.Round(sampleRate / powerlineFreq));

        _avgCycle = new double[_cycleLength];

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
        _cycleSlotsSaved = new double[SlotCount][];
        for (int i = 0; i < SlotCount; i++)
        {
            _cycleSlotsSaved[i] = new double[_cycleLength];
        }
    }

    /// <inheritdoc />
    public string Label => $"PLI-ECG {_powerlineFreq:F0}Hz fs={_sampleRate:F0}Hz";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        _sampleCount++;

        // Store sample into the current cycle slot
        _cycleSlots[_currentSlot][_cyclePos] = input;

        // Compute the averaged interference at this cycle position
        double output;

        if (_cyclesAccumulated >= 2)
        {
            // We have enough cycles to estimate the interference.
            // Subtract the averaged pattern from the input.
            output = input - _avgCycle[_cyclePos];
        }
        else
        {
            // During learning phase, pass through
            output = input;
        }

        _cyclePos++;

        if (_cyclePos >= _cycleLength)
        {
            // Completed one full cycle
            _cyclePos = 0;

            if (_cyclesAccumulated < SlotCount)
            {
                _cyclesAccumulated++;
            }

            // Move to next slot in the ring
            _currentSlot = (_currentSlot + 1) % SlotCount;

            // Recompute the average cycle from all accumulated slots
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
        var copy = new PlifEcgSubtractFilter(_powerlineFreq, _sampleRate);

        Array.Copy(_avgCycle, copy._avgCycle, _cycleLength);

        for (int i = 0; i < SlotCount; i++)
        {
            Array.Copy(_cycleSlots[i], copy._cycleSlots[i], _cycleLength);
        }

        copy._currentSlot = _currentSlot;
        copy._cyclePos = _cyclePos;
        copy._cyclesAccumulated = _cyclesAccumulated;
        copy._sampleCount = _sampleCount;

        // Copy saved state
        Array.Copy(_avgCycleSaved, copy._avgCycleSaved, _cycleLength);

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
