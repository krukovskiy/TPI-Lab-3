namespace EdfBrowser.Converters.Common;

/// <summary>
/// Contract for a format converter that can convert a biomedical signal file to/from EDF/BDF.
/// </summary>
public interface IFormatConverter
{
    /// <summary>Short name of the converter (e.g. "ASCII to EDF").</summary>
    string Name { get; }

    /// <summary>Human-readable description of what this converter does.</summary>
    string Description { get; }

    /// <summary>File extensions this converter can handle (e.g. [".csv", ".txt"]).</summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Converts the input file to the output file asynchronously.
    /// </summary>
    /// <param name="inputPath">Path to the source file.</param>
    /// <param name="outputPath">Path to the destination file.</param>
    /// <param name="options">Optional converter configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConvertAsync(
        string inputPath,
        string outputPath,
        ConverterOptions? options = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options that control how a format conversion is performed.
/// </summary>
public sealed class ConverterOptions
{
    /// <summary>Target sample rate in Hz. Required for formats that don't store it.</summary>
    public double? SampleRate { get; set; }

    /// <summary>Number of signal channels. Required for raw binary formats.</summary>
    public int? SignalCount { get; set; }

    /// <summary>Patient identification string for the EDF header.</summary>
    public string? PatientId { get; set; }

    /// <summary>Recording identification string for the EDF header.</summary>
    public string? RecordingId { get; set; }

    /// <summary>Recording start date/time.</summary>
    public DateTime? StartDateTime { get; set; }

    /// <summary>Column separator for ASCII/CSV files.</summary>
    public string? Separator { get; set; } = ",";

    /// <summary>Whether the ASCII file has a header row with channel labels.</summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>Number of initial lines to skip before data begins.</summary>
    public int? SkipLines { get; set; }

    /// <summary>Physical maximum value override.</summary>
    public double? PhysicalMax { get; set; }

    /// <summary>Physical minimum value override.</summary>
    public double? PhysicalMin { get; set; }

    /// <summary>Physical dimension / unit string (e.g. "uV", "mV").</summary>
    public string? PhysicalDimension { get; set; }

    /// <summary>Duration of one data record in seconds (default 1.0).</summary>
    public double? DataRecordDuration { get; set; } = 1.0;

    /// <summary>
    /// Data format for raw binary files. Supported values:
    /// "int16", "int32", "float32", "float64".
    /// </summary>
    public string? DataFormat { get; set; }

    /// <summary>
    /// Byte order for raw binary files. "little" or "big".
    /// </summary>
    public string? ByteOrder { get; set; } = "little";

    /// <summary>Whether to include a time column when exporting to ASCII.</summary>
    public bool IncludeTimeColumn { get; set; }

    /// <summary>Decimal precision for ASCII export.</summary>
    public int DecimalPrecision { get; set; } = 6;

    /// <summary>
    /// Filter chain configuration for filtered export.
    /// Keys are signal indices, values are filter description strings.
    /// </summary>
    public Dictionary<int, string>? FilterConfigurations { get; set; }

    /// <summary>Arbitrary key-value pairs for format-specific options.</summary>
    public Dictionary<string, string> CustomOptions { get; set; } = new();
}

/// <summary>
/// Reports the progress of a conversion operation.
/// </summary>
/// <param name="PercentComplete">Completion percentage (0-100).</param>
/// <param name="StatusMessage">Human-readable status message.</param>
public readonly record struct ConversionProgress(int PercentComplete, string StatusMessage);
