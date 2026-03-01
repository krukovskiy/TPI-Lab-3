namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// General second-order IIR (biquad) filter using Direct Form II Transposed implementation.
/// Used by <see cref="EdfBrowser.Dsp.Design.ButterworthDesigner"/> for cascaded sections.
/// </summary>
public sealed class BiquadFilter : IFilter, IFrequencyResponse
{
    private readonly double _b0, _b1, _b2, _a1, _a2;
    private double _z1, _z2;
    private double _z1Saved, _z2Saved;

    private bool _disposed;

    /// <summary>
    /// Constructs a biquad filter with the given coefficients.
    /// Coefficients assume a0 is already normalized to 1.
    /// </summary>
    public BiquadFilter(double b0, double b1, double b2, double a1, double a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }

    /// <inheritdoc />
    public string Label => $"Biquad b=[{_b0:G4},{_b1:G4},{_b2:G4}] a=[1,{_a1:G4},{_a2:G4}]";

    /// <inheritdoc />
    public double ProcessSample(double input)
    {
        double output = _b0 * input + _z1;
        _z1 = _b1 * input - _a1 * output + _z2;
        _z2 = _b2 * input - _a2 * output;
        return output;
    }

    /// <inheritdoc />
    public void Reset(double initialValue = 0.0)
    {
        _z1 = initialValue;
        _z2 = initialValue;
    }

    /// <inheritdoc />
    public void SaveState()
    {
        _z1Saved = _z1;
        _z2Saved = _z2;
    }

    /// <inheritdoc />
    public void RestoreState()
    {
        _z1 = _z1Saved;
        _z2 = _z2Saved;
    }

    /// <inheritdoc />
    public IFilter DeepCopy()
    {
        var copy = new BiquadFilter(_b0, _b1, _b2, _a1, _a2);
        copy._z1 = _z1;
        copy._z2 = _z2;
        copy._z1Saved = _z1Saved;
        copy._z2Saved = _z2Saved;
        return copy;
    }

    /// <inheritdoc />
    public double GetMagnitudeResponse(double frequencyHz, double sampleRate)
    {
        double omega = 2.0 * Math.PI * frequencyHz / sampleRate;
        double cosW = Math.Cos(omega);
        double cos2W = Math.Cos(2.0 * omega);
        double sinW = Math.Sin(omega);
        double sin2W = Math.Sin(2.0 * omega);

        double numReal = _b0 + _b1 * cosW + _b2 * cos2W;
        double numImag = -(_b1 * sinW + _b2 * sin2W);

        double denReal = 1.0 + _a1 * cosW + _a2 * cos2W;
        double denImag = -(_a1 * sinW + _a2 * sin2W);

        double numMagSq = numReal * numReal + numImag * numImag;
        double denMagSq = denReal * denReal + denImag * denImag;

        return denMagSq > 0.0 ? Math.Sqrt(numMagSq / denMagSq) : 0.0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}
