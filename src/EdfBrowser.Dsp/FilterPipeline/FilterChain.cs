namespace EdfBrowser.Dsp.FilterPipeline;

using EdfBrowser.Dsp.Filters;

/// <summary>
/// A chain of IFilter instances with optional pre- and post-processing math functions.
/// Samples flow through pre-math functions, then filters, then post-math functions.
/// </summary>
public sealed class FilterChain : IDisposable
{
    /// <summary>
    /// Maximum number of filters allowed in the chain.
    /// </summary>
    public const int MaxFilters = 16;

    /// <summary>
    /// Maximum number of math functions allowed in each pre/post list.
    /// </summary>
    public const int MaxMathFunctions = 16;

    private readonly List<MathFunction> _preMathFunctions = new();
    private readonly List<IFilter> _filters = new();
    private readonly List<MathFunction> _postMathFunctions = new();

    private bool _disposed;

    /// <summary>
    /// The filters in the processing chain, in order.
    /// </summary>
    public IReadOnlyList<IFilter> Filters => _filters;

    /// <summary>
    /// Math functions applied before the filter chain.
    /// </summary>
    public IReadOnlyList<MathFunction> PreMathFunctions => _preMathFunctions;

    /// <summary>
    /// Math functions applied after the filter chain.
    /// </summary>
    public IReadOnlyList<MathFunction> PostMathFunctions => _postMathFunctions;

    /// <summary>
    /// Appends a filter to the end of the chain.
    /// </summary>
    public void AddFilter(IFilter filter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(filter);

        if (_filters.Count >= MaxFilters)
            throw new InvalidOperationException($"Cannot exceed {MaxFilters} filters in the chain.");

        _filters.Add(filter);
    }

    /// <summary>
    /// Removes the filter at the specified index and disposes it.
    /// </summary>
    public void RemoveFilter(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _filters.Count);

        var filter = _filters[index];
        _filters.RemoveAt(index);
        filter.Dispose();
    }

    /// <summary>
    /// Inserts a filter at the specified position in the chain.
    /// </summary>
    public void InsertFilter(int index, IFilter filter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _filters.Count);

        if (_filters.Count >= MaxFilters)
            throw new InvalidOperationException($"Cannot exceed {MaxFilters} filters in the chain.");

        _filters.Insert(index, filter);
    }

    /// <summary>
    /// Removes and disposes all filters in the chain.
    /// </summary>
    public void ClearFilters()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var filter in _filters)
            filter.Dispose();

        _filters.Clear();
    }

    /// <summary>
    /// Adds a math function to be applied before the filter chain.
    /// </summary>
    public void AddPreMathFunction(MathFunction func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(func);

        if (_preMathFunctions.Count >= MaxMathFunctions)
            throw new InvalidOperationException($"Cannot exceed {MaxMathFunctions} pre-math functions.");

        _preMathFunctions.Add(func);
    }

    /// <summary>
    /// Adds a math function to be applied after the filter chain.
    /// </summary>
    public void AddPostMathFunction(MathFunction func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(func);

        if (_postMathFunctions.Count >= MaxMathFunctions)
            throw new InvalidOperationException($"Cannot exceed {MaxMathFunctions} post-math functions.");

        _postMathFunctions.Add(func);
    }

    /// <summary>
    /// Clears all pre- and post-math functions.
    /// </summary>
    public void ClearMathFunctions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _preMathFunctions.Clear();
        _postMathFunctions.Clear();
    }

    /// <summary>
    /// Processes a single sample through the entire chain:
    /// pre-math functions, then filters, then post-math functions.
    /// </summary>
    public double Process(double sample)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double value = sample;

        foreach (var math in _preMathFunctions)
            value = math.Apply(value);

        foreach (var filter in _filters)
            value = filter.ProcessSample(value);

        foreach (var math in _postMathFunctions)
            value = math.Apply(value);

        return value;
    }

    /// <summary>
    /// Saves the state of all filters in the chain.
    /// </summary>
    public void SaveState()
    {
        foreach (var f in _filters)
            f.SaveState();
    }

    /// <summary>
    /// Restores the previously saved state of all filters in the chain.
    /// </summary>
    public void RestoreState()
    {
        foreach (var f in _filters)
            f.RestoreState();
    }

    /// <summary>
    /// Resets all filters and math functions in the chain.
    /// </summary>
    public void Reset()
    {
        foreach (var f in _filters)
            f.Reset();

        foreach (var m in _preMathFunctions)
            m.Reset();

        foreach (var m in _postMathFunctions)
            m.Reset();
    }

    /// <summary>
    /// Creates a deep copy of the entire filter chain, including all filters and math functions.
    /// </summary>
    public FilterChain DeepCopy()
    {
        var copy = new FilterChain();

        foreach (var math in _preMathFunctions)
            copy._preMathFunctions.Add(math.DeepCopy());

        foreach (var filter in _filters)
            copy._filters.Add(filter.DeepCopy());

        foreach (var math in _postMathFunctions)
            copy._postMathFunctions.Add(math.DeepCopy());

        return copy;
    }

    /// <summary>
    /// Disposes all filters in the chain.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var filter in _filters)
            filter.Dispose();

        _filters.Clear();
        _preMathFunctions.Clear();
        _postMathFunctions.Clear();
    }
}
