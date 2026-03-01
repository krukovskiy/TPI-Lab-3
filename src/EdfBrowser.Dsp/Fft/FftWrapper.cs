namespace EdfBrowser.Dsp.Fft;

using System.Numerics;

/// <summary>
/// FFT implementation using the Cooley-Tukey radix-2 decimation-in-time algorithm.
/// Supports forward and inverse transforms, plus convenience methods for power
/// and magnitude spectra. No external dependencies are required.
/// </summary>
public sealed class FftWrapper
{
    private readonly int _fftSize;
    private readonly int _logN;

    /// <summary>
    /// The FFT size (must be a power of 2).
    /// </summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// The window type to apply to real input before computing spectra.
    /// </summary>
    public WindowType WindowType { get; set; }

    /// <summary>
    /// Constructs a new FFT wrapper for the given size.
    /// </summary>
    /// <param name="fftSize">Must be a positive power of 2.</param>
    /// <param name="windowType">Window function to apply to real input data.</param>
    public FftWrapper(int fftSize, WindowType windowType = WindowType.Hanning)
    {
        if (fftSize <= 0 || (fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a positive power of 2.", nameof(fftSize));

        _fftSize = fftSize;
        _logN = (int)Math.Log2(fftSize);
        WindowType = windowType;
    }

    /// <summary>
    /// Performs a forward (time-to-frequency) FFT in-place using the
    /// Cooley-Tukey radix-2 decimation-in-time algorithm.
    /// </summary>
    public void Forward(Span<Complex> data)
    {
        if (data.Length != _fftSize)
            throw new ArgumentException($"Data length must equal FFT size ({_fftSize}).", nameof(data));

        BitReversalPermutation(data);
        CooleyTukeyButterfly(data, inverse: false);
    }

    /// <summary>
    /// Performs an inverse (frequency-to-time) FFT in-place.
    /// The output is scaled by 1/N.
    /// </summary>
    public void Inverse(Span<Complex> data)
    {
        if (data.Length != _fftSize)
            throw new ArgumentException($"Data length must equal FFT size ({_fftSize}).", nameof(data));

        BitReversalPermutation(data);
        CooleyTukeyButterfly(data, inverse: true);

        // Scale by 1/N
        double scale = 1.0 / _fftSize;
        for (int i = 0; i < _fftSize; i++)
        {
            data[i] *= scale;
        }
    }

    /// <summary>
    /// Computes the power spectrum (|X(f)|^2) from real-valued input.
    /// Returns N/2+1 values corresponding to frequencies [0, sampleRate/2].
    /// </summary>
    public double[] ComputePowerSpectrum(ReadOnlySpan<double> input, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        var complexData = PrepareWindowed(input);

        Forward(complexData);

        int spectrumSize = _fftSize / 2 + 1;
        double[] power = new double[spectrumSize];
        double scale = 1.0 / ((double)_fftSize * _fftSize);

        for (int i = 0; i < spectrumSize; i++)
        {
            double re = complexData[i].Real;
            double im = complexData[i].Imaginary;
            power[i] = (re * re + im * im) * scale;

            // Double the power for non-DC, non-Nyquist bins (single-sided spectrum)
            if (i > 0 && i < _fftSize / 2)
                power[i] *= 2.0;
        }

        return power;
    }

    /// <summary>
    /// Computes the magnitude spectrum |X(f)| from real-valued input.
    /// Returns N/2+1 values corresponding to frequencies [0, sampleRate/2].
    /// </summary>
    public double[] ComputeMagnitudeSpectrum(ReadOnlySpan<double> input, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        var complexData = PrepareWindowed(input);

        Forward(complexData);

        int spectrumSize = _fftSize / 2 + 1;
        double[] magnitude = new double[spectrumSize];
        double scale = 1.0 / _fftSize;

        for (int i = 0; i < spectrumSize; i++)
        {
            magnitude[i] = complexData[i].Magnitude * scale;

            // Double for non-DC, non-Nyquist bins (single-sided spectrum)
            if (i > 0 && i < _fftSize / 2)
                magnitude[i] *= 2.0;
        }

        return magnitude;
    }

    /// <summary>
    /// Returns the frequency values (in Hz) for each bin of the spectrum.
    /// Returns N/2+1 values from 0 to sampleRate/2.
    /// </summary>
    public double[] GetFrequencyBins(double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        int spectrumSize = _fftSize / 2 + 1;
        double[] bins = new double[spectrumSize];
        double binWidth = sampleRate / _fftSize;

        for (int i = 0; i < spectrumSize; i++)
        {
            bins[i] = i * binWidth;
        }

        return bins;
    }

    /// <summary>
    /// Prepares windowed complex data from real input. If input is shorter than FFT size,
    /// it is zero-padded. If longer, it is truncated.
    /// </summary>
    private Complex[] PrepareWindowed(ReadOnlySpan<double> input)
    {
        double[] windowed = new double[_fftSize];
        int copyLen = Math.Min(input.Length, _fftSize);

        for (int i = 0; i < copyLen; i++)
            windowed[i] = input[i];

        WindowFunction.Apply(windowed.AsSpan(), WindowType);

        Complex[] complexData = new Complex[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            complexData[i] = new Complex(windowed[i], 0.0);
        }

        return complexData;
    }

    /// <summary>
    /// Performs bit-reversal permutation on the data array.
    /// </summary>
    private void BitReversalPermutation(Span<Complex> data)
    {
        for (int i = 1, j = 0; i < _fftSize; i++)
        {
            int bit = _fftSize >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;

            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
        }
    }

    /// <summary>
    /// Cooley-Tukey butterfly operations for radix-2 DIT FFT.
    /// </summary>
    private void CooleyTukeyButterfly(Span<Complex> data, bool inverse)
    {
        for (int size = 2; size <= _fftSize; size *= 2)
        {
            int halfSize = size / 2;
            double angleSign = inverse ? 1.0 : -1.0;
            double angle = angleSign * 2.0 * Math.PI / size;
            Complex wn = new(Math.Cos(angle), Math.Sin(angle));

            for (int start = 0; start < _fftSize; start += size)
            {
                Complex w = Complex.One;

                for (int k = 0; k < halfSize; k++)
                {
                    int evenIdx = start + k;
                    int oddIdx = start + k + halfSize;

                    Complex t = w * data[oddIdx];
                    Complex u = data[evenIdx];

                    data[evenIdx] = u + t;
                    data[oddIdx] = u - t;

                    w *= wn;
                }
            }
        }
    }
}
