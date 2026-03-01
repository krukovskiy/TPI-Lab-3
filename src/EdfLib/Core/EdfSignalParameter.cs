namespace EdfLib.Core;

/// <summary>
/// Per-signal parameters from the EDF/BDF header (replaces edfpar_t in the original C library).
/// </summary>
public sealed class EdfSignalParameter
{
    /// <summary>Signal label (max 16 chars, space-padded in the file).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Transducer type description (max 80 chars).</summary>
    public string TransducerType { get; set; } = string.Empty;

    /// <summary>Physical dimension / units (max 8 chars, e.g. "uV").</summary>
    public string PhysicalDimension { get; set; } = string.Empty;

    /// <summary>Prefiltering description (max 80 chars, e.g. "HP:0.1Hz LP:75Hz").</summary>
    public string Prefiltering { get; set; } = string.Empty;

    /// <summary>Physical (calibrated) minimum value.</summary>
    public double PhysicalMinimum { get; set; }

    /// <summary>Physical (calibrated) maximum value.</summary>
    public double PhysicalMaximum { get; set; }

    /// <summary>Digital (raw integer) minimum value.</summary>
    public int DigitalMinimum { get; set; }

    /// <summary>Digital (raw integer) maximum value.</summary>
    public int DigitalMaximum { get; set; }

    /// <summary>Number of samples in each data record for this signal.</summary>
    public int SamplesPerDataRecord { get; set; }

    /// <summary>Reserved field for this signal (max 32 chars).</summary>
    public string Reserved { get; set; } = string.Empty;

    /// <summary>
    /// Whether this signal is an annotation channel
    /// (label = "EDF Annotations" or "BDF Annotations").
    /// </summary>
    public bool IsAnnotationSignal { get; set; }

    // --- Computed properties ---

    /// <summary>
    /// Scaling factor to convert digital values to physical values.
    /// BitValue = (PhysicalMax - PhysicalMin) / (DigitalMax - DigitalMin).
    /// </summary>
    public double BitValue
    {
        get
        {
            double digitalRange = DigitalMaximum - (double)DigitalMinimum;
            if (digitalRange == 0.0) return 0.0;
            return (PhysicalMaximum - PhysicalMinimum) / digitalRange;
        }
    }

    /// <summary>
    /// Offset used in digital-to-physical conversion.
    /// Offset = PhysicalMax / BitValue - DigitalMax.
    /// Alternatively: physical = (digital + Offset) * BitValue.
    /// </summary>
    public double Offset
    {
        get
        {
            double bv = BitValue;
            if (bv == 0.0) return 0.0;
            return PhysicalMaximum / bv - DigitalMaximum;
        }
    }

    /// <summary>
    /// Computes the sample frequency for this signal given the data record duration.
    /// SampleFrequency = SamplesPerDataRecord / dataRecordDurationSeconds.
    /// </summary>
    /// <param name="dataRecordDurationSeconds">Duration of one data record in seconds.</param>
    /// <returns>The sample frequency in Hz.</returns>
    public double GetSampleFrequency(double dataRecordDurationSeconds)
    {
        if (dataRecordDurationSeconds <= 0.0) return 0.0;
        return SamplesPerDataRecord / dataRecordDurationSeconds;
    }

    /// <summary>Creates a deep copy of this signal parameter.</summary>
    public EdfSignalParameter Clone() => new()
    {
        Label = Label,
        TransducerType = TransducerType,
        PhysicalDimension = PhysicalDimension,
        Prefiltering = Prefiltering,
        PhysicalMinimum = PhysicalMinimum,
        PhysicalMaximum = PhysicalMaximum,
        DigitalMinimum = DigitalMinimum,
        DigitalMaximum = DigitalMaximum,
        SamplesPerDataRecord = SamplesPerDataRecord,
        Reserved = Reserved,
        IsAnnotationSignal = IsAnnotationSignal,
    };

    public override string ToString() =>
        $"{Label.Trim()} [{PhysicalDimension.Trim()}] {SamplesPerDataRecord} samp/rec " +
        $"Dig[{DigitalMinimum}..{DigitalMaximum}] Phys[{PhysicalMinimum}..{PhysicalMaximum}]";
}
