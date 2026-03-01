using System.Globalization;
using System.Text;

namespace EdfBrowser.Converters.Common;

/// <summary>
/// Base class for all format converters providing shared EDF/BDF header writing
/// and common validation/progress utilities.
/// </summary>
public abstract class ConverterBase : IFormatConverter
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract string[] SupportedExtensions { get; }

    /// <inheritdoc />
    public abstract Task ConvertAsync(
        string inputPath,
        string outputPath,
        ConverterOptions? options = null,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reports progress if a reporter is available.</summary>
    protected static void ReportProgress(IProgress<ConversionProgress>? progress, int percent, string message)
    {
        progress?.Report(new ConversionProgress(Math.Clamp(percent, 0, 100), message));
    }

    /// <summary>Validates that the input file exists and is accessible.</summary>
    protected static void ValidateInputFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Input file not found: {path}", path);
    }

    /// <summary>Validates that the output directory exists and the path is valid.</summary>
    protected static void ValidateOutputFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Output directory not found: {directory}");
    }

    /// <summary>
    /// Writes a complete EDF/BDF header to the given BinaryWriter.
    /// The writer position will be at the start of the data record area after this call.
    /// </summary>
    protected static void WriteEdfHeader(BinaryWriter writer, EdfHeaderData headerData)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(headerData);

        bool isBdf = headerData.IsBdf;
        int ns = headerData.SignalCount;
        int headerBytes = 256 + 256 * ns;

        // --- Fixed header (256 bytes) ---

        // Version (8 bytes)
        if (isBdf)
        {
            writer.Write((byte)0xFF);
            WriteAsciiField(writer, "BIOSEMI", 7);
        }
        else
        {
            WriteAsciiField(writer, headerData.Version, 8);
        }

        // Patient ID (80 bytes)
        WriteAsciiField(writer, headerData.PatientId, 80);

        // Recording ID (80 bytes)
        WriteAsciiField(writer, headerData.RecordingId, 80);

        // Start date (8 bytes: dd.mm.yy)
        DateTime dt = headerData.StartDateTime;
        int yy = dt.Year % 100;
        string dateStr = $"{dt.Day:D2}.{dt.Month:D2}.{yy:D2}";
        WriteAsciiField(writer, dateStr, 8);

        // Start time (8 bytes: hh.mm.ss)
        string timeStr = $"{dt.Hour:D2}.{dt.Minute:D2}.{dt.Second:D2}";
        WriteAsciiField(writer, timeStr, 8);

        // Number of bytes in header (8 bytes)
        WriteAsciiField(writer, headerBytes.ToString(CultureInfo.InvariantCulture), 8);

        // Reserved (44 bytes)
        string reserved = isBdf ? "BDF+C" : "";
        WriteAsciiField(writer, reserved, 44);

        // Number of data records (8 bytes)
        WriteAsciiField(writer, headerData.DataRecordCount.ToString(CultureInfo.InvariantCulture), 8);

        // Data record duration (8 bytes)
        WriteAsciiField(writer, headerData.DataRecordDuration.ToString("F6", CultureInfo.InvariantCulture), 8);

        // Number of signals (4 bytes)
        WriteAsciiField(writer, ns.ToString(CultureInfo.InvariantCulture), 4);

        // --- Per-signal headers ---

        // Labels (16 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.Label, 16);

        // Transducer types (80 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.TransducerType, 80);

        // Physical dimensions (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.PhysicalDimension, 8);

        // Physical minimums (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.PhysicalMin.ToString("G10", CultureInfo.InvariantCulture), 8);

        // Physical maximums (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.PhysicalMax.ToString("G10", CultureInfo.InvariantCulture), 8);

        // Digital minimums (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.DigitalMin.ToString(CultureInfo.InvariantCulture), 8);

        // Digital maximums (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.DigitalMax.ToString(CultureInfo.InvariantCulture), 8);

        // Prefiltering (80 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.Prefiltering, 80);

        // Samples per data record (8 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, sig.SamplesPerRecord.ToString(CultureInfo.InvariantCulture), 8);

        // Reserved (32 bytes each)
        foreach (var sig in headerData.Signals)
            WriteAsciiField(writer, "", 32);
    }

    /// <summary>
    /// Writes an EDF 16-bit sample to the writer.
    /// The value is clamped to [-32768, 32767].
    /// </summary>
    protected static void WriteEdfSample(BinaryWriter writer, int value)
    {
        short clamped = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        writer.Write(clamped);
    }

    /// <summary>
    /// Writes a BDF 24-bit sample to the writer in little-endian order.
    /// The value is clamped to [-8388608, 8388607].
    /// </summary>
    protected static void WriteBdfSample(BinaryWriter writer, int value)
    {
        int clamped = Math.Clamp(value, -8388608, 8388607);
        writer.Write((byte)(clamped & 0xFF));
        writer.Write((byte)((clamped >> 8) & 0xFF));
        writer.Write((byte)((clamped >> 16) & 0xFF));
    }

    /// <summary>
    /// Converts a physical value to a digital EDF sample using the given scaling parameters.
    /// </summary>
    protected static int PhysicalToDigital(double physicalValue, double physMin, double physMax, int digMin, int digMax)
    {
        double physRange = physMax - physMin;
        if (physRange == 0.0) return 0;
        double digRange = digMax - (double)digMin;
        double digital = (physicalValue - physMin) / physRange * digRange + digMin;
        return (int)Math.Round(Math.Clamp(digital, digMin, digMax));
    }

    /// <summary>
    /// Converts a digital sample value to a physical value using the given scaling parameters.
    /// </summary>
    protected static double DigitalToPhysical(int digitalValue, double physMin, double physMax, int digMin, int digMax)
    {
        double digRange = digMax - (double)digMin;
        if (digRange == 0.0) return 0.0;
        double physRange = physMax - physMin;
        return (digitalValue - digMin) / digRange * physRange + physMin;
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes from the stream.
    /// </summary>
    protected static byte[] ReadExactly(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0)
                throw new InvalidDataException(
                    $"Unexpected end of stream: expected {count} bytes, got {totalRead}.");
            totalRead += read;
        }
        return buffer;
    }

    /// <summary>
    /// Reads a 16-bit little-endian signed integer (EDF sample) from the stream.
    /// </summary>
    protected static int ReadEdfSample(BinaryReader reader)
    {
        return reader.ReadInt16();
    }

    /// <summary>
    /// Reads a 24-bit little-endian signed integer (BDF sample) from the stream.
    /// </summary>
    protected static int ReadBdfSample(BinaryReader reader)
    {
        byte b0 = reader.ReadByte();
        byte b1 = reader.ReadByte();
        byte b2 = reader.ReadByte();
        int value = b0 | (b1 << 8) | (b2 << 16);
        // Sign-extend from 24-bit
        if ((value & 0x800000) != 0)
            value |= unchecked((int)0xFF000000);
        return value;
    }

    /// <summary>
    /// Writes a fixed-width ASCII field, padded with spaces to the specified length.
    /// </summary>
    private static void WriteAsciiField(BinaryWriter writer, string value, int fieldLength)
    {
        value ??= string.Empty;
        if (value.Length > fieldLength)
            value = value[..fieldLength];

        byte[] bytes = Latin1.GetBytes(value);
        writer.Write(bytes);

        int padding = fieldLength - bytes.Length;
        for (int i = 0; i < padding; i++)
            writer.Write((byte)' ');
    }

    /// <summary>
    /// Header data structure used for building EDF/BDF headers.
    /// </summary>
    protected sealed class EdfHeaderData
    {
        /// <summary>EDF version string. Use "0" for EDF.</summary>
        public string Version { get; set; } = "0";

        /// <summary>Patient identification (80 chars max).</summary>
        public string PatientId { get; set; } = "";

        /// <summary>Recording identification (80 chars max).</summary>
        public string RecordingId { get; set; } = "";

        /// <summary>Recording start date/time.</summary>
        public DateTime StartDateTime { get; set; } = DateTime.Now;

        /// <summary>Number of signals in the file.</summary>
        public int SignalCount { get; set; }

        /// <summary>Duration of one data record in seconds.</summary>
        public double DataRecordDuration { get; set; } = 1.0;

        /// <summary>Total number of data records in the file.</summary>
        public long DataRecordCount { get; set; }

        /// <summary>Whether the output is BDF (24-bit) rather than EDF (16-bit).</summary>
        public bool IsBdf { get; set; }

        /// <summary>Per-signal parameters.</summary>
        public List<SignalData> Signals { get; set; } = new();
    }

    /// <summary>
    /// Per-signal data for building the EDF/BDF header.
    /// </summary>
    protected sealed class SignalData
    {
        /// <summary>Signal label (16 chars max).</summary>
        public string Label { get; set; } = "";

        /// <summary>Transducer type (80 chars max).</summary>
        public string TransducerType { get; set; } = "";

        /// <summary>Physical dimension / units (8 chars max).</summary>
        public string PhysicalDimension { get; set; } = "uV";

        /// <summary>Physical minimum value.</summary>
        public double PhysicalMin { get; set; } = -3200;

        /// <summary>Physical maximum value.</summary>
        public double PhysicalMax { get; set; } = 3200;

        /// <summary>Digital minimum value (-32768 for EDF, -8388608 for BDF).</summary>
        public int DigitalMin { get; set; } = -32768;

        /// <summary>Digital maximum value (32767 for EDF, 8388607 for BDF).</summary>
        public int DigitalMax { get; set; } = 32767;

        /// <summary>Prefiltering description (80 chars max).</summary>
        public string Prefiltering { get; set; } = "";

        /// <summary>Number of samples per data record for this signal.</summary>
        public int SamplesPerRecord { get; set; }
    }
}
