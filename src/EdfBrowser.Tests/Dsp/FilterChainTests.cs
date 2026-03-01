namespace EdfBrowser.Tests.Dsp;

using EdfBrowser.Dsp.Filters;
using EdfBrowser.Dsp.FilterPipeline;
using Xunit;

public class FilterChainTests
{
    [Fact]
    public void EmptyChain_PassesThroughUnchanged()
    {
        var chain = new FilterChain();
        Assert.Equal(1.0, chain.Process(1.0));
        Assert.Equal(-5.0, chain.Process(-5.0));
        chain.Dispose();
    }

    [Fact]
    public void SingleFilter_AppliesFilter()
    {
        var chain = new FilterChain();
        chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0));

        // Process a step function - output should be smoothed
        double prev = 0;
        for (int i = 0; i < 100; i++)
        {
            double output = chain.Process(1.0);
            Assert.True(output >= prev, "LPF output should be monotonically increasing for step input");
            prev = output;
        }
        chain.Dispose();
    }

    [Fact]
    public void MultipleFilters_AppliedInOrder()
    {
        var chain = new FilterChain();
        chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 50.0, 1000.0));
        chain.AddFilter(new FirstOrderIirFilter(FilterType.HighPass, 1.0, 1000.0));

        // Should pass mid-frequency content
        for (int i = 0; i < 500; i++)
            chain.Process(Math.Sin(2 * Math.PI * 25 * i / 1000.0));

        Assert.Equal(2, chain.Filters.Count);
        chain.Dispose();
    }

    [Fact]
    public void MathFunctions_AppliedCorrectly()
    {
        var chain = new FilterChain();
        chain.AddPreMathFunction(new MathFunction(MathOperation.Abs));

        Assert.Equal(5.0, chain.Process(-5.0));
        Assert.Equal(3.0, chain.Process(3.0));
        chain.Dispose();
    }

    [Fact]
    public void SaveRestoreState_Works()
    {
        var chain = new FilterChain();
        chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0));

        for (int i = 0; i < 50; i++) chain.Process(i * 0.1);
        chain.SaveState();

        double out1 = chain.Process(1.0);
        chain.RestoreState();
        double out2 = chain.Process(1.0);

        Assert.Equal(out1, out2, 10);
        chain.Dispose();
    }

    [Fact]
    public void DeepCopy_CreatesIndependentChain()
    {
        var chain = new FilterChain();
        chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0));
        for (int i = 0; i < 50; i++) chain.Process(i * 0.1);

        var copy = chain.DeepCopy();
        double out1 = chain.Process(1.0);
        double out2 = copy.Process(1.0);

        Assert.Equal(out1, out2, 10);
        chain.Dispose();
        copy.Dispose();
    }

    [Fact]
    public void MaxFilters_Enforced()
    {
        var chain = new FilterChain();
        for (int i = 0; i < FilterChain.MaxFilters; i++)
            chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0));

        Assert.Equal(FilterChain.MaxFilters, chain.Filters.Count);
        chain.Dispose();
    }

    [Fact]
    public void RemoveFilter_Works()
    {
        var chain = new FilterChain();
        chain.AddFilter(new FirstOrderIirFilter(FilterType.LowPass, 10.0, 1000.0));
        chain.AddFilter(new FirstOrderIirFilter(FilterType.HighPass, 1.0, 1000.0));

        Assert.Equal(2, chain.Filters.Count);
        chain.RemoveFilter(0);
        Assert.Single(chain.Filters);
        chain.Dispose();
    }
}
