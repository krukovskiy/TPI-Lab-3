namespace EdfBrowser.Dsp.Design;

using EdfBrowser.Dsp.Fft;

/// <summary>
/// Enumeration of FIR filter response types.
/// </summary>
public enum FirFilterType
{
    LowPass,
    HighPass,
    BandPass,
    BandStop
}

/// <summary>
/// Designs FIR filters using the windowed-sinc method.
/// All methods return coefficient arrays suitable for use with <see cref="EdfBrowser.Dsp.Filters.FirFilter"/>.
/// </summary>
public static class FirDesigner
{
    /// <summary>
    /// Designs a low-pass FIR filter using the windowed-sinc method.
    /// </summary>
    /// <param name="order">Filter order (number of taps = order + 1). Must be even.</param>
    /// <param name="cutoffHz">Cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Sampling rate in Hz.</param>
    /// <param name="window">Window function type to apply.</param>
    /// <returns>Array of filter coefficients.</returns>
    public static double[] DesignLowPass(int order, double cutoffHz, double sampleRate,
        WindowType window = WindowType.Blackman)
    {
        ValidateParameters(order, sampleRate);
        ValidateFrequency(cutoffHz, sampleRate);

        double normalizedCutoff = cutoffHz / sampleRate;
        int length = order + 1;
        double[] coefficients = new double[length];
        int middle = order / 2;

        // Generate sinc function for low-pass
        for (int i = 0; i < length; i++)
        {
            int n = i - middle;
            if (n == 0)
            {
                coefficients[i] = 2.0 * normalizedCutoff;
            }
            else
            {
                double x = 2.0 * Math.PI * normalizedCutoff * n;
                coefficients[i] = Math.Sin(x) / (Math.PI * n);
            }
        }

        // Apply window and normalize
        ApplyWindowAndNormalize(coefficients, window);

        return coefficients;
    }

    /// <summary>
    /// Designs a high-pass FIR filter using spectral inversion of a low-pass filter.
    /// </summary>
    /// <param name="order">Filter order (number of taps = order + 1). Must be even.</param>
    /// <param name="cutoffHz">Cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Sampling rate in Hz.</param>
    /// <param name="window">Window function type to apply.</param>
    /// <returns>Array of filter coefficients.</returns>
    public static double[] DesignHighPass(int order, double cutoffHz, double sampleRate,
        WindowType window = WindowType.Blackman)
    {
        ValidateParameters(order, sampleRate);
        ValidateFrequency(cutoffHz, sampleRate);

        // Design a low-pass filter first
        double[] coefficients = DesignLowPass(order, cutoffHz, sampleRate, window);

        // Spectral inversion: negate all coefficients and add 1 to the center tap
        int middle = order / 2;
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = -coefficients[i];
        }
        coefficients[middle] += 1.0;

        return coefficients;
    }

    /// <summary>
    /// Designs a band-pass FIR filter by convolving a low-pass and a high-pass filter.
    /// </summary>
    /// <param name="order">Filter order (number of taps = order + 1). Must be even.</param>
    /// <param name="lowCutoffHz">Lower cutoff frequency in Hz.</param>
    /// <param name="highCutoffHz">Upper cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Sampling rate in Hz.</param>
    /// <param name="window">Window function type to apply.</param>
    /// <returns>Array of filter coefficients.</returns>
    public static double[] DesignBandPass(int order, double lowCutoffHz, double highCutoffHz,
        double sampleRate, WindowType window = WindowType.Blackman)
    {
        ValidateParameters(order, sampleRate);
        ValidateBandFrequencies(lowCutoffHz, highCutoffHz, sampleRate);

        double normalizedLow = lowCutoffHz / sampleRate;
        double normalizedHigh = highCutoffHz / sampleRate;
        int length = order + 1;
        double[] coefficients = new double[length];
        int middle = order / 2;

        // Band-pass = low-pass(high) - low-pass(low)
        for (int i = 0; i < length; i++)
        {
            int n = i - middle;
            if (n == 0)
            {
                coefficients[i] = 2.0 * (normalizedHigh - normalizedLow);
            }
            else
            {
                double xHigh = 2.0 * Math.PI * normalizedHigh * n;
                double xLow = 2.0 * Math.PI * normalizedLow * n;
                coefficients[i] = (Math.Sin(xHigh) - Math.Sin(xLow)) / (Math.PI * n);
            }
        }

        ApplyWindowAndNormalize(coefficients, window);

        return coefficients;
    }

    /// <summary>
    /// Designs a band-stop (notch) FIR filter using spectral inversion of a band-pass filter.
    /// </summary>
    /// <param name="order">Filter order (number of taps = order + 1). Must be even.</param>
    /// <param name="lowCutoffHz">Lower cutoff frequency in Hz.</param>
    /// <param name="highCutoffHz">Upper cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Sampling rate in Hz.</param>
    /// <param name="window">Window function type to apply.</param>
    /// <returns>Array of filter coefficients.</returns>
    public static double[] DesignBandStop(int order, double lowCutoffHz, double highCutoffHz,
        double sampleRate, WindowType window = WindowType.Blackman)
    {
        ValidateParameters(order, sampleRate);
        ValidateBandFrequencies(lowCutoffHz, highCutoffHz, sampleRate);

        // Design a band-pass filter first
        double[] coefficients = DesignBandPass(order, lowCutoffHz, highCutoffHz, sampleRate, window);

        // Spectral inversion: negate all and add 1 to center tap
        int middle = order / 2;
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = -coefficients[i];
        }
        coefficients[middle] += 1.0;

        return coefficients;
    }

    private static void ApplyWindowAndNormalize(double[] coefficients, WindowType window)
    {
        // Apply the window function
        WindowFunction.Apply(coefficients.AsSpan(), window);

        // Normalize so that the sum of coefficients equals 1 (unity gain at DC)
        double sum = 0.0;
        for (int i = 0; i < coefficients.Length; i++)
            sum += coefficients[i];

        if (Math.Abs(sum) > 1e-15)
        {
            double invSum = 1.0 / sum;
            for (int i = 0; i < coefficients.Length; i++)
                coefficients[i] *= invSum;
        }
    }

    private static void ValidateParameters(int order, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(order, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0.0);

        if (order % 2 != 0)
            throw new ArgumentException("Filter order must be even for Type I FIR filter.", nameof(order));
    }

    private static void ValidateFrequency(double frequencyHz, double sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequencyHz, 0.0);

        if (frequencyHz >= sampleRate / 2.0)
            throw new ArgumentException(
                $"Cutoff frequency ({frequencyHz} Hz) must be less than Nyquist frequency ({sampleRate / 2.0} Hz).",
                nameof(frequencyHz));
    }

    private static void ValidateBandFrequencies(double lowHz, double highHz, double sampleRate)
    {
        ValidateFrequency(lowHz, sampleRate);
        ValidateFrequency(highHz, sampleRate);

        if (lowHz >= highHz)
            throw new ArgumentException(
                $"Low cutoff ({lowHz} Hz) must be less than high cutoff ({highHz} Hz).",
                nameof(lowHz));
    }
}
