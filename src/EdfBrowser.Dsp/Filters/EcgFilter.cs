namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Port of EDFbrowser's ecg_filter.c.
/// ECG-specific filter that detects QRS complexes and tracks heart rate.
/// Implements a Pan-Tompkins-style QRS detection algorithm:
///   1. Bandpass filter (8-20 Hz) to isolate QRS
///   2. Derivative filter to detect steep slopes
///   3. Squaring operation to amplify QRS
///   4. Moving window integration
///   5. Adaptive threshold with refractory period
/// </summary>
public sealed class EcgFilter : IFilter
{
    // Bandpass filter parameters
    private const double BandpassLow = 8.0;
    private const double BandpassHigh = 20.0;

    // Moving window integration size (150 ms worth of samples)
    private const double IntegrationWindowMs = 150.0;

    // Refractory period (~200 ms) - minimum interval between detected beats
    private const double RefractoryPeriodMs = 200.0;

    private readonly double _sampleRate;
    private readonly int _integrationWindowSize;
    private readonly int _refractoryPeriod;

    // Bandpass filter (cascaded low-pass and high-pass)
    private readonly FirstOrderIirFilter _lpFilter;
    private readonly FirstOrderIirFilter _hpFilter;

    // Derivative buffer (5-point derivative)
    private readonly double[] _derivBuf;
    private int _derivIdx;

    // Moving window integrator
    private readonly double[] _mwiBuffer;
    private int _mwiIdx;
    private double _mwiSum;

    // Adaptive threshold
    private double _threshold;
    private double _peakValue;
    private double _signalLevel;
    private double _noiseLevel;

    // QRS detection state
    private int _refractoryCounter;
    private long _sampleCounter;
    private long _lastQrsSample;
    private double _bpm;
    private bool _qrsDetected;

    // Saved state
    private double[] _derivBufSaved;
    private int _derivIdxSaved;
    private double[] _mwiBufSaved;
    private int _mwiIdxSaved;
    private double _mwiSumSaved;
    private double _thresholdSaved;
    private double _peakValueSaved;
    private double _signalLevelSaved;
    private double _noiseLevelSaved;
    private int _refractoryCounterSaved;
    private long _sampleCounterSaved;
    private long _lastQrsSampleSaved;
    private double _bpmSaved;

    private bool _disposed;

    /// <summary>
    /// Current estimated heart rate in beats per minute.
    /// </summary>
    public double Bpm => _bpm;

    /// <summary>
    /// Total number of samples processed.
    /// </summary>
    public long SmplCounter => _sampleCounter;

    /// <summary>
    /// Whether a QRS complex was detected on the last processed sample.
    /// </summary>
    public bool QrsDetected => _qrsDetected;

    /// <summary>
    /// Constructs an ECG QRS detection filter.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public EcgFilter(double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        _sampleRate = sampleRate;
        _integrationWindowSize = Math.Max(1, (int)(sampleRate * IntegrationWindowMs / 1000.0));
        _refractoryPeriod = Math.Max(1, (int)(sampleRate * RefractoryPeriodMs / 1000.0));

        // Bandpass: cascade LP at 20 Hz and HP at 8 Hz
        _lpFilter = new FirstOrderIirFilter(FilterType.LowPass, BandpassHigh, sampleRate);
        _hpFilter = new FirstOrderIirFilter(FilterType.HighPass, BandpassLow, sampleRate);

        // 5-point derivative buffer
        _derivBuf = new double[5];
        _derivIdx = 0;

        // Moving window integrator
        _mwiBuffer = new double[_integrationWindowSize];
        _mwiIdx = 0;
        _mwiSum = 0.0;

        // Initialize adaptive threshold parameters
        _threshold = 0.0;
        _peakValue = 0.0;
        _signalLevel = 0.0;
        _noiseLevel = 0.0;

        _refractoryCounter = 0;
        _sampleCounter = 0;
        _lastQrsSample = 0;
        _bpm = 0.0;
        _qrsDetected = false;

        // Allocate saved state buffers
        _derivBufSaved = new double[5];
        _mwiBufSaved = new double[_integrationWindowSize];
    }

    /// <inheritdoc />
    public string Label => $"ECG QRS fs={_sampleRate:F0}Hz";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        _qrsDetected = false;
        _sampleCounter++;

        // Stage 1: Bandpass filter
        double filtered = _lpFilter.ProcessSample(input);
        filtered = _hpFilter.ProcessSample(filtered);

        // Stage 2: Five-point derivative
        // y[n] = (1/8) * (-x[n-2] - 2*x[n-1] + 2*x[n+1] + x[n+2])
        // Using a delay line, we approximate with available past samples:
        // y[n] = (-buf[n-4] - 2*buf[n-3] + 2*buf[n-1] + buf[n]) / 8
        _derivBuf[_derivIdx] = filtered;
        int i0 = (_derivIdx + 5 - 4) % 5;
        int i1 = (_derivIdx + 5 - 3) % 5;
        int i3 = (_derivIdx + 5 - 1) % 5;
        int i4 = _derivIdx;

        double derivative = (-_derivBuf[i0] - 2.0 * _derivBuf[i1] + 2.0 * _derivBuf[i3] + _derivBuf[i4]) / 8.0;
        _derivIdx = (_derivIdx + 1) % 5;

        // Stage 3: Squaring
        double squared = derivative * derivative;

        // Stage 4: Moving window integration
        _mwiSum -= _mwiBuffer[_mwiIdx];
        _mwiBuffer[_mwiIdx] = squared;
        _mwiSum += squared;
        _mwiIdx = (_mwiIdx + 1) % _integrationWindowSize;

        double mwiOutput = _mwiSum / _integrationWindowSize;

        // Stage 5: Adaptive threshold and QRS detection
        if (_refractoryCounter > 0)
        {
            _refractoryCounter--;
        }
        else
        {
            if (mwiOutput > _threshold)
            {
                // Potential QRS detected
                _qrsDetected = true;
                _refractoryCounter = _refractoryPeriod;

                // Update signal level estimate
                _signalLevel = 0.125 * mwiOutput + 0.875 * _signalLevel;

                // Compute BPM from inter-beat interval
                long interval = _sampleCounter - _lastQrsSample;
                if (interval > 0 && _lastQrsSample > 0)
                {
                    double instantBpm = 60.0 * _sampleRate / interval;
                    // Clamp to physiological range
                    if (instantBpm >= 20.0 && instantBpm <= 300.0)
                    {
                        _bpm = 0.2 * instantBpm + 0.8 * _bpm;
                    }
                }

                _lastQrsSample = _sampleCounter;
            }
            else
            {
                // Update noise level estimate
                _noiseLevel = 0.125 * mwiOutput + 0.875 * _noiseLevel;
            }
        }

        // Track peak for threshold initialization
        if (mwiOutput > _peakValue)
        {
            _peakValue = mwiOutput;
        }

        // Update adaptive threshold
        _threshold = _noiseLevel + 0.25 * (_signalLevel - _noiseLevel);

        // During startup, use a fraction of peak as threshold
        if (_sampleCounter < (long)(2.0 * _sampleRate) && _threshold == 0.0)
        {
            _threshold = 0.5 * _peakValue;
        }

        return mwiOutput;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        _lpFilter.Reset(initialValue);
        _hpFilter.Reset(initialValue);
        Array.Fill(_derivBuf, 0.0);
        _derivIdx = 0;
        Array.Fill(_mwiBuffer, 0.0);
        _mwiIdx = 0;
        _mwiSum = 0.0;
        _threshold = 0.0;
        _peakValue = 0.0;
        _signalLevel = 0.0;
        _noiseLevel = 0.0;
        _refractoryCounter = 0;
        _sampleCounter = 0;
        _lastQrsSample = 0;
        _bpm = 0.0;
        _qrsDetected = false;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        _lpFilter.SaveState();
        _hpFilter.SaveState();
        Array.Copy(_derivBuf, _derivBufSaved, 5);
        _derivIdxSaved = _derivIdx;
        Array.Copy(_mwiBuffer, _mwiBufSaved, _integrationWindowSize);
        _mwiIdxSaved = _mwiIdx;
        _mwiSumSaved = _mwiSum;
        _thresholdSaved = _threshold;
        _peakValueSaved = _peakValue;
        _signalLevelSaved = _signalLevel;
        _noiseLevelSaved = _noiseLevel;
        _refractoryCounterSaved = _refractoryCounter;
        _sampleCounterSaved = _sampleCounter;
        _lastQrsSampleSaved = _lastQrsSample;
        _bpmSaved = _bpm;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        _lpFilter.RestoreState();
        _hpFilter.RestoreState();
        Array.Copy(_derivBufSaved, _derivBuf, 5);
        _derivIdx = _derivIdxSaved;
        Array.Copy(_mwiBufSaved, _mwiBuffer, _integrationWindowSize);
        _mwiIdx = _mwiIdxSaved;
        _mwiSum = _mwiSumSaved;
        _threshold = _thresholdSaved;
        _peakValue = _peakValueSaved;
        _signalLevel = _signalLevelSaved;
        _noiseLevel = _noiseLevelSaved;
        _refractoryCounter = _refractoryCounterSaved;
        _sampleCounter = _sampleCounterSaved;
        _lastQrsSample = _lastQrsSampleSaved;
        _bpm = _bpmSaved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new EcgFilter(_sampleRate);

        // Copy bandpass state via save/restore
        _lpFilter.SaveState();
        _hpFilter.SaveState();

        // We need to process enough samples to match the state.
        // Instead, directly copy internal fields.
        Array.Copy(_derivBuf, copy._derivBuf, 5);
        copy._derivIdx = _derivIdx;
        Array.Copy(_mwiBuffer, copy._mwiBuffer, _integrationWindowSize);
        copy._mwiIdx = _mwiIdx;
        copy._mwiSum = _mwiSum;
        copy._threshold = _threshold;
        copy._peakValue = _peakValue;
        copy._signalLevel = _signalLevel;
        copy._noiseLevel = _noiseLevel;
        copy._refractoryCounter = _refractoryCounter;
        copy._sampleCounter = _sampleCounter;
        copy._lastQrsSample = _lastQrsSample;
        copy._bpm = _bpm;
        copy._qrsDetected = _qrsDetected;

        // Copy saved state
        Array.Copy(_derivBufSaved, copy._derivBufSaved, 5);
        copy._derivIdxSaved = _derivIdxSaved;
        Array.Copy(_mwiBufSaved, copy._mwiBufSaved, _integrationWindowSize);
        copy._mwiIdxSaved = _mwiIdxSaved;
        copy._mwiSumSaved = _mwiSumSaved;
        copy._thresholdSaved = _thresholdSaved;
        copy._peakValueSaved = _peakValueSaved;
        copy._signalLevelSaved = _signalLevelSaved;
        copy._noiseLevelSaved = _noiseLevelSaved;
        copy._refractoryCounterSaved = _refractoryCounterSaved;
        copy._sampleCounterSaved = _sampleCounterSaved;
        copy._lastQrsSampleSaved = _lastQrsSampleSaved;
        copy._bpmSaved = _bpmSaved;

        return copy;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _lpFilter.Dispose();
            _hpFilter.Dispose();
            _disposed = true;
        }
    }
}
