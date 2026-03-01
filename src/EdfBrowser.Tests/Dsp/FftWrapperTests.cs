namespace EdfBrowser.Tests.Dsp;

using EdfBrowser.Dsp.Fft;
using Xunit;

public class FftWrapperTests
{
    [Fact]
    public void PowerSpectrum_PeakAtCorrectFrequency()
    {
        int fftSize = 1024;
        double sampleRate = 1000.0;
        double signalFreq = 100.0;

        var fft = new FftWrapper(fftSize, WindowType.Rectangular);
        var input = new double[fftSize];
        for (int i = 0; i < fftSize; i++)
            input[i] = Math.Sin(2 * Math.PI * signalFreq * i / sampleRate);

        var spectrum = fft.ComputePowerSpectrum(input, sampleRate);
        var freqs = fft.GetFrequencyBins(sampleRate);

        // Find peak
        int peakIdx = 0;
        double peakVal = 0;
        for (int i = 1; i < spectrum.Length; i++)
        {
            if (spectrum[i] > peakVal)
            {
                peakVal = spectrum[i];
                peakIdx = i;
            }
        }

        double peakFreq = freqs[peakIdx];
        Assert.True(Math.Abs(peakFreq - signalFreq) < sampleRate / fftSize * 2,
            $"Peak should be near {signalFreq} Hz, got {peakFreq} Hz");
    }

    [Fact]
    public void MagnitudeSpectrum_NonNegative()
    {
        var fft = new FftWrapper(256);
        var input = new double[256];
        var rng = new Random(42);
        for (int i = 0; i < 256; i++) input[i] = rng.NextDouble() - 0.5;

        var spectrum = fft.ComputeMagnitudeSpectrum(input, 1000.0);
        foreach (var val in spectrum)
            Assert.True(val >= 0, "Magnitude spectrum values should be non-negative");
    }

    [Fact]
    public void FrequencyBins_CorrectRange()
    {
        int fftSize = 512;
        double sampleRate = 1000.0;
        var fft = new FftWrapper(fftSize);
        var bins = fft.GetFrequencyBins(sampleRate);

        Assert.Equal(fftSize / 2 + 1, bins.Length);
        Assert.Equal(0.0, bins[0]);
        Assert.True(Math.Abs(bins[^1] - sampleRate / 2.0) < 1.0,
            $"Last bin should be near Nyquist ({sampleRate / 2}), got {bins[^1]}");
    }

    [Fact]
    public void WindowFunction_Rectangular_IsIdentity()
    {
        var data = new double[] { 1, 2, 3, 4 };
        var copy = (double[])data.Clone();
        WindowFunction.Apply(copy, WindowType.Rectangular);
        for (int i = 0; i < data.Length; i++)
            Assert.Equal(data[i], copy[i]);
    }

    [Fact]
    public void WindowFunction_Hanning_EndsAtZero()
    {
        int size = 256;
        var window = WindowFunction.Create(size, WindowType.Hanning);
        Assert.Equal(0.0, window[0], 6);
        Assert.Equal(0.0, window[size - 1], 6);
    }
}
