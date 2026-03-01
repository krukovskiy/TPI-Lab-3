namespace EdfLib.Core;

/// <summary>
/// Constants matching the original edflib C library from EDFbrowser.
/// </summary>
public static class EdfConstants
{
    // --- Capacity limits ---

    /// <summary>Maximum number of files that can be open simultaneously.</summary>
    public const int MaxFiles = 32;

    /// <summary>Maximum number of signals (channels) per file.</summary>
    public const int MaxSignals = 4096;

    /// <summary>Maximum length of an annotation description in characters.</summary>
    public const int MaxAnnotationLength = 512;

    // --- Time scaling ---

    /// <summary>
    /// Fixed-point scaling factor for time values: 10 000 000 ticks per second (100 ns resolution).
    /// Matches TIME_FIXP_SCALING in the original C library.
    /// </summary>
    public const long TimeFixedPointScaling = 10_000_000L;

    // --- Header field sizes (bytes) ---

    /// <summary>Size of the fixed (non-signal) portion of the header in bytes.</summary>
    public const int FixedHeaderSize = 256;

    /// <summary>Size of one signal's header block in bytes.</summary>
    public const int SignalHeaderSize = 256;

    /// <summary>Version field: 8 bytes.</summary>
    public const int VersionFieldSize = 8;

    /// <summary>Patient identification field: 80 bytes.</summary>
    public const int PatientIdFieldSize = 80;

    /// <summary>Recording identification field: 80 bytes.</summary>
    public const int RecordingIdFieldSize = 80;

    /// <summary>Start date field (dd.mm.yy): 8 bytes.</summary>
    public const int StartDateFieldSize = 8;

    /// <summary>Start time field (hh.mm.ss): 8 bytes.</summary>
    public const int StartTimeFieldSize = 8;

    /// <summary>Number of bytes in header field: 8 bytes.</summary>
    public const int HeaderBytesFieldSize = 8;

    /// <summary>Reserved field in the fixed header: 44 bytes.</summary>
    public const int ReservedFieldSize = 44;

    /// <summary>Number of data records field: 8 bytes.</summary>
    public const int DataRecordCountFieldSize = 8;

    /// <summary>Data record duration field: 8 bytes.</summary>
    public const int DataRecordDurationFieldSize = 8;

    /// <summary>Number of signals field: 4 bytes.</summary>
    public const int SignalCountFieldSize = 4;

    // --- Per-signal header field sizes (bytes) ---

    /// <summary>Signal label: 16 bytes.</summary>
    public const int LabelFieldSize = 16;

    /// <summary>Transducer type: 80 bytes.</summary>
    public const int TransducerTypeFieldSize = 80;

    /// <summary>Physical dimension (units): 8 bytes.</summary>
    public const int PhysicalDimensionFieldSize = 8;

    /// <summary>Physical minimum: 8 bytes.</summary>
    public const int PhysicalMinFieldSize = 8;

    /// <summary>Physical maximum: 8 bytes.</summary>
    public const int PhysicalMaxFieldSize = 8;

    /// <summary>Digital minimum: 8 bytes.</summary>
    public const int DigitalMinFieldSize = 8;

    /// <summary>Digital maximum: 8 bytes.</summary>
    public const int DigitalMaxFieldSize = 8;

    /// <summary>Prefiltering: 80 bytes.</summary>
    public const int PrefilteringFieldSize = 80;

    /// <summary>Number of samples per data record: 8 bytes.</summary>
    public const int SamplesPerRecordFieldSize = 8;

    /// <summary>Signal reserved field: 32 bytes.</summary>
    public const int SignalReservedFieldSize = 32;

    // --- Sample sizes ---

    /// <summary>EDF sample size in bytes (16-bit signed integer).</summary>
    public const int EdfSampleSizeBytes = 2;

    /// <summary>BDF sample size in bytes (24-bit signed integer).</summary>
    public const int BdfSampleSizeBytes = 3;

    // --- Digital range limits ---

    /// <summary>Maximum digital value for EDF (16-bit): 32767.</summary>
    public const int EdfDigitalMax = 32767;

    /// <summary>Minimum digital value for EDF (16-bit): -32768.</summary>
    public const int EdfDigitalMin = -32768;

    /// <summary>Maximum digital value for BDF (24-bit): 8388607.</summary>
    public const int BdfDigitalMax = 8388607;

    /// <summary>Minimum digital value for BDF (24-bit): -8388608.</summary>
    public const int BdfDigitalMin = -8388608;

    // --- Special markers ---

    /// <summary>BDF version byte: 0xFF.</summary>
    public const byte BdfVersionByte = 0xFF;

    /// <summary>TAL onset/duration separator: 0x15 (NAK).</summary>
    public const byte TalDurationSeparator = 0x15;

    /// <summary>TAL field separator: 0x14 (DC4).</summary>
    public const byte TalFieldSeparator = 0x14;

    /// <summary>TAL terminator: 0x00 (NUL).</summary>
    public const byte TalTerminator = 0x00;

    // --- Well-known signal labels ---

    /// <summary>Annotation channel label for EDF+.</summary>
    public const string EdfAnnotationsLabel = "EDF Annotations";

    /// <summary>Annotation channel label for BDF+.</summary>
    public const string BdfAnnotationsLabel = "BDF Annotations";

    // --- Reserved field markers ---

    /// <summary>EDF+ continuous recording marker.</summary>
    public const string EdfPlusContinuous = "EDF+C";

    /// <summary>EDF+ discontinuous recording marker.</summary>
    public const string EdfPlusDiscontinuous = "EDF+D";

    /// <summary>BDF+ continuous recording marker.</summary>
    public const string BdfPlusContinuous = "BDF+C";

    /// <summary>BDF+ discontinuous recording marker.</summary>
    public const string BdfPlusDiscontinuous = "BDF+D";
}
