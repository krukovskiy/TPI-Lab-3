namespace EdfBrowser.Tests.Dsp;

using EdfBrowser.Dsp.Filters;
using Xunit;

public class FirstOrderIirFilterTests
{
    [Fact]
    public void LowPassFilter_AttenuatesHighFrequencies()
    {
        var filter = new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0);

        // Feed DC (0 Hz) - should pass through
        double dc = 0;
        for (int i = 0; i < 1000; i++)
            dc = filter.ProcessSample(1.0);
        Assert.True(dc > 0.99, $"DC should pass through LPF, got {dc}");

        // Reset and feed high-frequency signal
        filter.Reset();
        double maxHigh = 0;
        for (int i = 0; i < 1000; i++)
        {
            double input = Math.Sin(2 * Math.PI * 400 * i / 1000.0); // 400 Hz
            double output = filter.ProcessSample(input);
            maxHigh = Math.Max(maxHigh, Math.Abs(output));
        }
        Assert.True(maxHigh < 0.1, $"400 Hz should be attenuated by LPF(10Hz), got {maxHigh}");
    }

    [Fact]
    public void HighPassFilter_AttenuatesLowFrequencies()
    {
        var filter = new FirstOrderIirFilter(FilterType.HighPass, 100.0, 1000.0);

        // Feed DC (0 Hz) - should be blocked
        double dc = 0;
        for (int i = 0; i < 1000; i++)
            dc = filter.ProcessSample(1.0);
        Assert.True(Math.Abs(dc) < 0.01, $"DC should be blocked by HPF, got {dc}");
    }

    [Fact]
    public void SaveRestore_PreservesState()
    {
        var filter = new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0);

        for (int i = 0; i < 100; i++)
            filter.ProcessSample(Math.Sin(i * 0.1));

        filter.SaveState();
        double afterSave = filter.ProcessSample(0.5);

        filter.RestoreState();
        double afterRestore = filter.ProcessSample(0.5);

        Assert.Equal(afterSave, afterRestore, 10);
    }

    [Fact]
    public void DeepCopy_CreatesIndependentFilter()
    {
        var filter1 = new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0);
        for (int i = 0; i < 50; i++) filter1.ProcessSample(i * 0.1);

        var filter2 = (FirstOrderIirFilter)filter1.DeepCopy();
        double out1 = filter1.ProcessSample(1.0);
        double out2 = filter2.ProcessSample(1.0);

        Assert.Equal(out1, out2, 10);
    }

    [Fact]
    public void Label_ContainsFilterInfo()
    {
        var filter = new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0);
        Assert.Contains("LowPass", filter.Label);
    }
}
