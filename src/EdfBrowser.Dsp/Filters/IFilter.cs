namespace EdfBrowser.Dsp.Filters;

/// <summary>
/// Common interface for all digital signal processing filters.
/// </summary>
public interface IFilter : IDisposable
{
    /// <summary>
    /// Processes a single input sample and returns the filtered output.
    /// </summary>
    double ProcessSample(double input);

    /// <summary>
    /// Resets the filter state to the specified initial value.
    /// </summary>
    void Reset(double initialValue = 0.0);

    /// <summary>
    /// Saves the current internal state so it can be restored later.
    /// </summary>
    void SaveState();

    /// <summary>
    /// Restores the internal state previously saved by <see cref="SaveState"/>.
    /// </summary>
    void RestoreState();

    /// <summary>
    /// Creates a deep copy of this filter with identical configuration and state.
    /// </summary>
    IFilter DeepCopy();

    /// <summary>
    /// Human-readable label describing the filter configuration.
    /// </summary>
    string Label { get; }
}

/// <summary>
/// Optional interface for filters that can report their frequency response.
/// </summary>
public interface IFrequencyResponse
{
    /// <summary>
    /// Returns the magnitude response at the given frequency.
    /// </summary>
    double GetMagnitudeResponse(double frequencyHz, double sampleRate);
}
