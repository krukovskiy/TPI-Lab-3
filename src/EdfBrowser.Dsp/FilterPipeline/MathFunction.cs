namespace EdfBrowser.Dsp.FilterPipeline;

/// <summary>
/// Enumeration of supported mathematical operations that can be applied to signal samples.
/// </summary>
public enum MathOperation
{
    None,
    Abs,
    Sqrt,
    Square,
    Log,
    Ln,
    Invert,
    Differentiate,
    Integrate
}

/// <summary>
/// Applies a single mathematical function to a signal sample.
/// Stateful operations (Differentiate, Integrate) maintain internal accumulators.
/// </summary>
public sealed class MathFunction
{
    /// <summary>
    /// The mathematical operation this instance applies.
    /// </summary>
    public MathOperation Operation { get; }

    private double _previousValue;
    private double _accumulator;

    /// <summary>
    /// Constructs a new math function for the specified operation.
    /// </summary>
    public MathFunction(MathOperation operation)
    {
        Operation = operation;
    }

    /// <summary>
    /// Applies the configured mathematical operation to the input value.
    /// </summary>
    public double Apply(double input)
    {
        return Operation switch
        {
            MathOperation.None => input,
            MathOperation.Abs => Math.Abs(input),
            MathOperation.Sqrt => input >= 0.0 ? Math.Sqrt(input) : -Math.Sqrt(-input),
            MathOperation.Square => input * input,
            MathOperation.Log => input > 0.0 ? Math.Log10(input) : 0.0,
            MathOperation.Ln => input > 0.0 ? Math.Log(input) : 0.0,
            MathOperation.Invert => input != 0.0 ? 1.0 / input : 0.0,
            MathOperation.Differentiate => Differentiate(input),
            MathOperation.Integrate => Integrate(input),
            _ => input
        };
    }

    private double Differentiate(double input)
    {
        double result = input - _previousValue;
        _previousValue = input;
        return result;
    }

    private double Integrate(double input)
    {
        _accumulator += input;
        return _accumulator;
    }

    /// <summary>
    /// Resets internal state for stateful operations (Differentiate, Integrate).
    /// </summary>
    public void Reset()
    {
        _previousValue = 0.0;
        _accumulator = 0.0;
    }

    /// <summary>
    /// Creates a deep copy of this math function with a fresh state.
    /// </summary>
    public MathFunction DeepCopy() => new(Operation);
}
