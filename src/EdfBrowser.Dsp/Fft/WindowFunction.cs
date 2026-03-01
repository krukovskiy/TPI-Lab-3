namespace EdfBrowser.Dsp.Fft;

/// <summary>
/// Enumeration of supported window function types for spectral analysis.
/// </summary>
public enum WindowType
{
    Rectangular,
    Hanning,
    Hamming,
    Blackman,
    BlackmanHarris,
    FlatTop,
    Nuttall,
    Nuttall3b,
    Nuttall4c
}

/// <summary>
/// Provides standard window functions for spectral analysis and FIR filter design.
/// </summary>
public static class WindowFunction
{
    /// <summary>
    /// Applies the specified window function in-place to the data span.
    /// </summary>
    public static void Apply(Span<double> data, WindowType type)
    {
        if (type == WindowType.Rectangular)
            return;

        int n = data.Length;
        for (int i = 0; i < n; i++)
        {
            data[i] *= ComputeCoefficient(i, n, type);
        }
    }

    /// <summary>
    /// Creates a window of the specified size and type.
    /// </summary>
    public static double[] Create(int size, WindowType type)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);

        double[] window = new double[size];

        if (type == WindowType.Rectangular)
        {
            Array.Fill(window, 1.0);
            return window;
        }

        for (int i = 0; i < size; i++)
        {
            window[i] = ComputeCoefficient(i, size, type);
        }

        return window;
    }

    private static double ComputeCoefficient(int i, int n, WindowType type)
    {
        double nMinus1 = n - 1;
        if (nMinus1 <= 0.0)
            return 1.0;

        double phase = 2.0 * Math.PI * i / nMinus1;

        return type switch
        {
            WindowType.Rectangular => 1.0,

            // Hanning: 0.5 - 0.5 * cos(2*pi*n / (N-1))
            WindowType.Hanning =>
                0.5 - 0.5 * Math.Cos(phase),

            // Hamming: 0.54 - 0.46 * cos(2*pi*n / (N-1))
            WindowType.Hamming =>
                0.54 - 0.46 * Math.Cos(phase),

            // Blackman: 0.42 - 0.5 * cos(2*pi*n/(N-1)) + 0.08 * cos(4*pi*n/(N-1))
            WindowType.Blackman =>
                0.42 - 0.5 * Math.Cos(phase) + 0.08 * Math.Cos(2.0 * phase),

            // Blackman-Harris: 4-term
            // a0 = 0.35875, a1 = 0.48829, a2 = 0.14128, a3 = 0.01168
            WindowType.BlackmanHarris =>
                0.35875
                - 0.48829 * Math.Cos(phase)
                + 0.14128 * Math.Cos(2.0 * phase)
                - 0.01168 * Math.Cos(3.0 * phase),

            // Flat Top (ISO 18431-2)
            // a0 = 0.21557895, a1 = 0.41663158, a2 = 0.277263158, a3 = 0.083578947, a4 = 0.006947368
            WindowType.FlatTop =>
                0.21557895
                - 0.41663158 * Math.Cos(phase)
                + 0.277263158 * Math.Cos(2.0 * phase)
                - 0.083578947 * Math.Cos(3.0 * phase)
                + 0.006947368 * Math.Cos(4.0 * phase),

            // Nuttall: 4-term with continuous first derivative
            // a0 = 0.355768, a1 = 0.487396, a2 = 0.144232, a3 = 0.012604
            WindowType.Nuttall =>
                0.355768
                - 0.487396 * Math.Cos(phase)
                + 0.144232 * Math.Cos(2.0 * phase)
                - 0.012604 * Math.Cos(3.0 * phase),

            // Nuttall 3-term, variant b (Nuttall3b)
            // a0 = 0.4243801, a1 = 0.4973406, a2 = 0.0782793
            WindowType.Nuttall3b =>
                0.4243801
                - 0.4973406 * Math.Cos(phase)
                + 0.0782793 * Math.Cos(2.0 * phase),

            // Nuttall 4-term, variant c (Nuttall4c)
            // a0 = 0.3635819, a1 = 0.4891775, a2 = 0.1365995, a3 = 0.0106411
            WindowType.Nuttall4c =>
                0.3635819
                - 0.4891775 * Math.Cos(phase)
                + 0.1365995 * Math.Cos(2.0 * phase)
                - 0.0106411 * Math.Cos(3.0 * phase),

            _ => 1.0
        };
    }
}
