using System.Globalization;

namespace EdfLib.Core;

/// <summary>
/// Full header model for an EDF/BDF file (replaces edfhdrblck_t in the original C library).
/// </summary>
public sealed class EdfHeader
{
    // --- Format identification ---

    /// <summary>The specific EDF/BDF variant.</summary>
    public EdfFileFormat Format { get; set; }

    /// <summary>Version string from the header ("0" for EDF, 0xFF-prefixed for BDF).</summary>
    public string Version { get; set; } = string.Empty;

    // --- Patient / Recording identification (raw) ---

    /// <summary>Patient identification string (80 chars in file).</summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Recording identification string (80 chars in file).</summary>
    public string RecordingId { get; set; } = string.Empty;

    // --- Date / Time ---

    /// <summary>Start date and time of the recording (second precision from the header).</summary>
    public DateTime StartDateTime { get; set; }

    /// <summary>
    /// Sub-second offset of the start time in 100-nanosecond ticks.
    /// Parsed from the EDF+/BDF+ recording identification "Startdate" field subsecond component.
    /// </summary>
    public long StartDateTimeSubSecond { get; set; }

    // --- Data layout ---

    /// <summary>Total number of signals (channels) in the file, including annotation channels.</summary>
    public int SignalCount { get; set; }

    /// <summary>
    /// Number of data records in the file. May be -1 if unknown (streaming),
    /// which is legal in EDF+ but must be updated when the file is closed.
    /// </summary>
    public long DataRecordCount { get; set; }

    /// <summary>Duration of one data record in seconds (e.g. 1.0).</summary>
    public double DataRecordDuration { get; set; }

    /// <summary>Total size of the header in bytes (256 + 256 * SignalCount).</summary>
    public int HeaderSizeInBytes { get; set; }

    // --- EDF+/BDF+ specific ---

    /// <summary>
    /// Whether the recording is continuous (EDF+C / BDF+C) or discontinuous (EDF+D / BDF+D).
    /// Only meaningful for EDF+/BDF+ files; always <c>true</c> for plain EDF/BDF.
    /// </summary>
    public bool IsContinuous { get; set; } = true;

    /// <summary>The reserved field from the fixed header (44 bytes).</summary>
    public string Reserved { get; set; } = string.Empty;

    // --- Signals ---

    /// <summary>Per-signal parameters.</summary>
    public List<EdfSignalParameter> Signals { get; set; } = [];

    // --- Structured patient info (EDF+ sub-fields) ---

    /// <summary>Patient code (EDF+ patient field, first sub-field).</summary>
    public string PatientCode { get; set; } = string.Empty;

    /// <summary>Patient sex: "M", "F", or "X" (EDF+ patient field, second sub-field).</summary>
    public string PatientSex { get; set; } = string.Empty;

    /// <summary>Patient birth date (EDF+ patient field, third sub-field), or null if not specified.</summary>
    public DateTime? PatientBirthDate { get; set; }

    /// <summary>Patient name (EDF+ patient field, fourth sub-field).</summary>
    public string PatientName { get; set; } = string.Empty;

    // --- Structured recording info (EDF+ sub-fields) ---

    /// <summary>Recording start date from the EDF+ recording identification field.</summary>
    public DateTime? RecordingStartDate { get; set; }

    /// <summary>Administration code (EDF+ recording field, second sub-field after "Startdate").</summary>
    public string RecordingAdminCode { get; set; } = string.Empty;

    /// <summary>Technician name (EDF+ recording field, third sub-field).</summary>
    public string RecordingTechnician { get; set; } = string.Empty;

    /// <summary>Equipment identifier (EDF+ recording field, fourth sub-field).</summary>
    public string RecordingEquipment { get; set; } = string.Empty;

    // --- Computed properties ---

    /// <summary>Total recording duration as an <see cref="EdfTime"/>.</summary>
    public EdfTime Duration
    {
        get
        {
            if (DataRecordCount <= 0) return EdfTime.Zero;
            return EdfTime.FromSeconds(DataRecordCount * DataRecordDuration);
        }
    }

    /// <summary>
    /// Returns the number of ordinary (non-annotation) signals.
    /// </summary>
    public int OrdinarySignalCount => Signals.Count(s => !s.IsAnnotationSignal);

    /// <summary>
    /// Whether the file format uses 24-bit (BDF) samples rather than 16-bit (EDF).
    /// </summary>
    public bool IsBdf => Format is EdfFileFormat.Bdf or EdfFileFormat.BdfPlus;

    /// <summary>
    /// The byte size of a single sample: 2 for EDF, 3 for BDF.
    /// </summary>
    public int SampleSizeInBytes => IsBdf ? EdfConstants.BdfSampleSizeBytes : EdfConstants.EdfSampleSizeBytes;

    /// <summary>
    /// Size of one complete data record in bytes (sum of all signals' sample counts * sample size).
    /// </summary>
    public int DataRecordSizeInBytes
    {
        get
        {
            int totalSamples = 0;
            foreach (var sig in Signals)
                totalSamples += sig.SamplesPerDataRecord;
            return totalSamples * SampleSizeInBytes;
        }
    }

    /// <summary>
    /// Returns the signal parameters for ordinary (non-annotation) signals only,
    /// preserving original indices as a list of (originalIndex, parameter) tuples.
    /// </summary>
    public IReadOnlyList<(int OriginalIndex, EdfSignalParameter Parameter)> GetOrdinarySignals()
    {
        var result = new List<(int, EdfSignalParameter)>();
        for (int i = 0; i < Signals.Count; i++)
        {
            if (!Signals[i].IsAnnotationSignal)
                result.Add((i, Signals[i]));
        }
        return result;
    }

    public override string ToString()
    {
        string fmt = Format.ToString();
        string continuous = IsContinuous ? "continuous" : "discontinuous";
        string duration = Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        return $"{fmt} ({continuous}), {SignalCount} signals, {DataRecordCount} records, {duration}s total";
    }
}
