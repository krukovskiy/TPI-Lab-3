namespace EdfBrowser.Tests.EdfLib;

using EdfLib.Core;
using Xunit;

public class EdfTimeTests
{
    [Fact]
    public void FromSeconds_ReturnsCorrectTicks()
    {
        var time = EdfTime.FromSeconds(1.0);
        Assert.Equal(10_000_000L, time.Ticks);
    }

    [Fact]
    public void FromSeconds_FractionalSeconds()
    {
        var time = EdfTime.FromSeconds(0.5);
        Assert.Equal(5_000_000L, time.Ticks);
    }

    [Fact]
    public void TotalSeconds_ReturnsCorrectValue()
    {
        var time = new EdfTime(30_000_000L); // 3 seconds
        Assert.Equal(3.0, time.TotalSeconds, 6);
    }

    [Fact]
    public void Addition_Works()
    {
        var a = EdfTime.FromSeconds(1.0);
        var b = EdfTime.FromSeconds(2.0);
        var result = a + b;
        Assert.Equal(3.0, result.TotalSeconds, 6);
    }

    [Fact]
    public void Subtraction_Works()
    {
        var a = EdfTime.FromSeconds(5.0);
        var b = EdfTime.FromSeconds(2.0);
        var result = a - b;
        Assert.Equal(3.0, result.TotalSeconds, 6);
    }

    [Fact]
    public void Comparison_Works()
    {
        var a = EdfTime.FromSeconds(1.0);
        var b = EdfTime.FromSeconds(2.0);
        Assert.True(a < b);
        Assert.True(b > a);
        Assert.False(a == b);
    }

    [Fact]
    public void Zero_IsDefault()
    {
        var time = EdfTime.Zero;
        Assert.Equal(0L, time.Ticks);
        Assert.Equal(0.0, time.TotalSeconds);
    }

    [Fact]
    public void ToTimeSpan_ReturnsCorrectValue()
    {
        var time = EdfTime.FromSeconds(10.5);
        var ts = time.ToTimeSpan();
        Assert.Equal(10.5, ts.TotalSeconds, 6);
    }

    [Fact]
    public void FromMilliseconds_ReturnsCorrectTicks()
    {
        var time = EdfTime.FromMilliseconds(500);
        Assert.Equal(5_000_000L, time.Ticks);
    }

    [Fact]
    public void Equality_WorksCorrectly()
    {
        var a = EdfTime.FromSeconds(1.0);
        var b = new EdfTime(10_000_000L);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
