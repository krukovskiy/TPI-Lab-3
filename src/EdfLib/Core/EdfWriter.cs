using System.Globalization;
using System.Text;

namespace EdfLib.Core;

/// <summary>
/// Write API for creating EDF/BDF files. Samples are written one signal at a time,
/// cycling through all signals to form complete data records.
/// </summary>
public sealed class EdfWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly EdfFileFormat _format;
    private readonly int _signalCount;
    private readonly EdfSignalParameter[] _signals;
    private bool _headerWritten;
    private long _dataRecordCount;
    private int _currentSampleIndex; // tracks which signal's samples to write next
    private readonly List<EdfAnnotation> _pendingAnnotations;
    private string _patientId = "";
    private string _recordingId = "";
    private DateTime _startDateTime = DateTime.Now;
    private long _startSubSecond;
    private double _dataRecordDuration = 1.0;
    private bool _disposed;

    // For EDF+/BDF+ we auto-add an annotation channel
    private int _annotationSignalIndex = -1;
    private bool _hasAnnotationChannel;

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Creates a new EDF/BDF writer.
    /// </summary>
    /// <param name="filePath">Output file path.</param>
    /// <param name="format">The EDF/BDF format variant to write.</param>
    /// <param name="signalCount">Number of ordinary (non-annotation) signals.</param>
    public EdfWriter(string filePath, EdfFileFormat format, int signalCount)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (signalCount <= 0 || signalCount > EdfConstants.MaxSignals)
            throw new ArgumentOutOfRangeException(nameof(signalCount),
                $"Signal count must be between 1 and {EdfConstants.MaxSignals}.");

        _stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _format = format;
        _signalCount = signalCount;
        _signals = new EdfSignalParameter[signalCount];
        _pendingAnnotations = new List<EdfAnnotation>();

        for (int i = 0; i < signalCount; i++)
        {
            _signals[i] = new EdfSignalParameter
            {
                DigitalMinimum = IsBdf ? EdfConstants.BdfDigitalMin : EdfConstants.EdfDigitalMin,
                DigitalMaximum = IsBdf ? EdfConstants.BdfDigitalMax : EdfConstants.EdfDigitalMax,
                PhysicalMinimum = -1.0,
                PhysicalMaximum = 1.0,
                SamplesPerDataRecord = 1,
                PhysicalDimension = "",
                Label = "",
                TransducerType = "",
                Prefiltering = "",
            };
        }
    }

    private bool IsBdf => _format is EdfFileFormat.Bdf or EdfFileFormat.BdfPlus;
    private bool IsPlus => _format is EdfFileFormat.EdfPlus or EdfFileFormat.BdfPlus;

    // -----------------------------------------------------------------------
    // Configuration methods (must call before first write)
    // -----------------------------------------------------------------------

    /// <summary>Sets the number of samples per data record for a signal.</summary>
    public void SetSampleFrequency(int signal, int samplesPerDataRecord)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);
        if (samplesPerDataRecord <= 0)
            throw new ArgumentOutOfRangeException(nameof(samplesPerDataRecord), "Must be positive.");
        _signals[signal].SamplesPerDataRecord = samplesPerDataRecord;
    }

    /// <summary>Sets the physical minimum, maximum, and dimension (units) for a signal.</summary>
    public void SetPhysicalParameters(int signal, double min, double max, string dimension)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);
        _signals[signal].PhysicalMinimum = min;
        _signals[signal].PhysicalMaximum = max;
        _signals[signal].PhysicalDimension = dimension ?? "";
    }

    /// <summary>Sets the digital minimum and maximum for a signal.</summary>
    public void SetDigitalParameters(int signal, int min, int max)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);

        if (IsBdf)
        {
            if (min < EdfConstants.BdfDigitalMin || max > EdfConstants.BdfDigitalMax)
                throw new ArgumentOutOfRangeException(
                    $"Digital range [{min}..{max}] exceeds BDF limits [{EdfConstants.BdfDigitalMin}..{EdfConstants.BdfDigitalMax}].");
        }
        else
        {
            if (min < EdfConstants.EdfDigitalMin || max > EdfConstants.EdfDigitalMax)
                throw new ArgumentOutOfRangeException(
                    $"Digital range [{min}..{max}] exceeds EDF limits [{EdfConstants.EdfDigitalMin}..{EdfConstants.EdfDigitalMax}].");
        }

        _signals[signal].DigitalMinimum = min;
        _signals[signal].DigitalMaximum = max;
    }

    /// <summary>Sets the label for a signal.</summary>
    public void SetLabel(int signal, string label)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);
        _signals[signal].Label = label ?? "";
    }

    /// <summary>Sets the transducer type for a signal.</summary>
    public void SetTransducerType(int signal, string transducerType)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);
        _signals[signal].TransducerType = transducerType ?? "";
    }

    /// <summary>Sets the prefiltering description for a signal.</summary>
    public void SetPrefiltering(int signal, string prefiltering)
    {
        ThrowIfHeaderWritten();
        ValidateSignalIndex(signal);
        _signals[signal].Prefiltering = prefiltering ?? "";
    }

    /// <summary>Sets the patient identification string.</summary>
    public void SetPatientInfo(string patientId)
    {
        ThrowIfHeaderWritten();
        _patientId = patientId ?? "";
    }

    /// <summary>Sets the recording identification string.</summary>
    public void SetRecordingInfo(string recordingId)
    {
        ThrowIfHeaderWritten();
        _recordingId = recordingId ?? "";
    }

    /// <summary>Sets the recording start date and time.</summary>
    /// <param name="dateTime">Start date/time (second precision).</param>
    /// <param name="subSecond100ns">Sub-second offset in 100-ns ticks.</param>
    public void SetStartDateTime(DateTime dateTime, long subSecond100ns = 0)
    {
        ThrowIfHeaderWritten();
        _startDateTime = dateTime;
        _startSubSecond = subSecond100ns;
    }

    /// <summary>Sets the data record duration in seconds.</summary>
    public void SetDataRecordDuration(double seconds)
    {
        ThrowIfHeaderWritten();
        if (seconds <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Duration must be positive.");
        _dataRecordDuration = seconds;
    }

    // -----------------------------------------------------------------------
    // Writing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes physical samples for the current signal. The caller must cycle through
    /// all signals in order. The buffer length must equal <c>SamplesPerDataRecord</c>
    /// for the current signal.
    /// </summary>
    /// <param name="buffer">Physical sample values to write.</param>
    public void WritePhysicalSamples(double[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ThrowIfDisposed();
        EnsureHeaderWritten();

        int sigIdx = GetCurrentWriteSignalIndex();
        var param = GetWriteSignalParameter(sigIdx);

        if (buffer.Length != param.SamplesPerDataRecord)
            throw new ArgumentException(
                $"Expected {param.SamplesPerDataRecord} samples for signal {_currentSampleIndex}, got {buffer.Length}.",
                nameof(buffer));

        double bitValue = param.BitValue;
        double offset = param.Offset;
        int sampleSize = IsBdf ? EdfConstants.BdfSampleSizeBytes : EdfConstants.EdfSampleSizeBytes;

        for (int i = 0; i < buffer.Length; i++)
        {
            // Convert physical to digital: digital = (physical / bitValue) - offset
            int digital;
            if (bitValue == 0.0)
            {
                digital = 0;
            }
            else
            {
                double rawDigital = (buffer[i] / bitValue) - offset;
                digital = (int)Math.Round(rawDigital);
            }

            // Clamp to valid range
            digital = Math.Clamp(digital, param.DigitalMinimum, param.DigitalMaximum);

            WriteSample(digital, sampleSize);
        }

        AdvanceSignalCursor();
    }

    /// <summary>
    /// Writes digital (raw integer) samples for the current signal. The caller must cycle
    /// through all signals in order. The buffer length must equal <c>SamplesPerDataRecord</c>
    /// for the current signal.
    /// </summary>
    /// <param name="buffer">Digital sample values to write.</param>
    public void WriteDigitalSamples(int[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ThrowIfDisposed();
        EnsureHeaderWritten();

        int sigIdx = GetCurrentWriteSignalIndex();
        var param = GetWriteSignalParameter(sigIdx);

        if (buffer.Length != param.SamplesPerDataRecord)
            throw new ArgumentException(
                $"Expected {param.SamplesPerDataRecord} samples for signal {_currentSampleIndex}, got {buffer.Length}.",
                nameof(buffer));

        int sampleSize = IsBdf ? EdfConstants.BdfSampleSizeBytes : EdfConstants.EdfSampleSizeBytes;

        for (int i = 0; i < buffer.Length; i++)
        {
            int digital = Math.Clamp(buffer[i], param.DigitalMinimum, param.DigitalMaximum);
            WriteSample(digital, sampleSize);
        }

        AdvanceSignalCursor();
    }

    /// <summary>
    /// Adds an annotation to be written. Annotations are accumulated and written
    /// into the annotation channel of subsequent data records.
    /// </summary>
    /// <param name="onset">Onset time relative to recording start.</param>
    /// <param name="duration">Duration, or <c>null</c> if unspecified.</param>
    /// <param name="description">Annotation text.</param>
    public void WriteAnnotation(EdfTime onset, EdfTime? duration, string description)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(description);
        _pendingAnnotations.Add(new EdfAnnotation(onset, duration, description));
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finalizes the file: seeks back to update the data record count in the header,
    /// then closes the stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_headerWritten && _stream.CanSeek && _stream.CanWrite)
            {
                // Update data record count at bytes 236-243 (8-byte ASCII field)
                // Offset: version(8) + patient(80) + recording(80) + date(8) + time(8) + headerBytes(8) + reserved(44) = 236
                const int dataRecordCountOffset = EdfConstants.VersionFieldSize
                    + EdfConstants.PatientIdFieldSize
                    + EdfConstants.RecordingIdFieldSize
                    + EdfConstants.StartDateFieldSize
                    + EdfConstants.StartTimeFieldSize
                    + EdfConstants.HeaderBytesFieldSize
                    + EdfConstants.ReservedFieldSize;

                _stream.Seek(dataRecordCountOffset, SeekOrigin.Begin);
                WriteAsciiField(_dataRecordCount.ToString(CultureInfo.InvariantCulture),
                    EdfConstants.DataRecordCountFieldSize);
            }
        }
        finally
        {
            _stream.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // Header writing
    // -----------------------------------------------------------------------

    private void EnsureHeaderWritten()
    {
        if (_headerWritten) return;

        // For EDF+/BDF+ formats, add an annotation channel if not already present
        if (IsPlus)
        {
            _hasAnnotationChannel = true;
        }

        WriteHeader();
        _headerWritten = true;
    }

    private void WriteHeader()
    {
        _stream.Position = 0;

        // Total number of signals in file (ordinary + annotation if needed)
        int totalSignals = _signalCount + (_hasAnnotationChannel ? 1 : 0);

        // Build the complete signal list including annotation channel
        var allSignals = new List<EdfSignalParameter>();
        for (int i = 0; i < _signalCount; i++)
            allSignals.Add(_signals[i]);

        if (_hasAnnotationChannel)
        {
            _annotationSignalIndex = allSignals.Count;
            var annParam = new EdfSignalParameter
            {
                Label = IsBdf ? EdfConstants.BdfAnnotationsLabel : EdfConstants.EdfAnnotationsLabel,
                TransducerType = "",
                PhysicalDimension = "",
                PhysicalMinimum = -1.0,
                PhysicalMaximum = 1.0,
                DigitalMinimum = IsBdf ? EdfConstants.BdfDigitalMin : EdfConstants.EdfDigitalMin,
                DigitalMaximum = IsBdf ? EdfConstants.BdfDigitalMax : EdfConstants.EdfDigitalMax,
                SamplesPerDataRecord = 30, // 30 samples * 2 bytes = 60 bytes for annotations (or 90 for BDF)
                Prefiltering = "",
                IsAnnotationSignal = true,
            };
            allSignals.Add(annParam);
        }

        int headerSize = EdfConstants.FixedHeaderSize + EdfConstants.SignalHeaderSize * totalSignals;

        // --- Fixed header (256 bytes) ---

        // Version (8 bytes)
        if (IsBdf)
        {
            _stream.WriteByte(EdfConstants.BdfVersionByte);
            WriteAsciiField("BIOSEMI", EdfConstants.VersionFieldSize - 1);
        }
        else
        {
            WriteAsciiField("0", EdfConstants.VersionFieldSize);
        }

        // Patient ID (80 bytes)
        WriteAsciiField(_patientId, EdfConstants.PatientIdFieldSize);

        // Recording ID (80 bytes)
        WriteAsciiField(_recordingId, EdfConstants.RecordingIdFieldSize);

        // Start date (8 bytes: dd.mm.yy)
        int year2 = _startDateTime.Year % 100;
        string dateStr = string.Format(CultureInfo.InvariantCulture,
            "{0:D2}.{1:D2}.{2:D2}", _startDateTime.Day, _startDateTime.Month, year2);
        WriteAsciiField(dateStr, EdfConstants.StartDateFieldSize);

        // Start time (8 bytes: hh.mm.ss)
        string timeStr = string.Format(CultureInfo.InvariantCulture,
            "{0:D2}.{1:D2}.{2:D2}", _startDateTime.Hour, _startDateTime.Minute, _startDateTime.Second);
        WriteAsciiField(timeStr, EdfConstants.StartTimeFieldSize);

        // Header size (8 bytes)
        WriteAsciiField(headerSize.ToString(CultureInfo.InvariantCulture),
            EdfConstants.HeaderBytesFieldSize);

        // Reserved (44 bytes) - format marker for EDF+/BDF+
        string reserved = _format switch
        {
            EdfFileFormat.EdfPlus => EdfConstants.EdfPlusContinuous,
            EdfFileFormat.BdfPlus => EdfConstants.BdfPlusContinuous,
            _ => ""
        };
        WriteAsciiField(reserved, EdfConstants.ReservedFieldSize);

        // Data record count (8 bytes) - placeholder, updated on Dispose
        WriteAsciiField("-1", EdfConstants.DataRecordCountFieldSize);

        // Data record duration (8 bytes)
        WriteAsciiField(_dataRecordDuration.ToString(CultureInfo.InvariantCulture),
            EdfConstants.DataRecordDurationFieldSize);

        // Number of signals (4 bytes)
        WriteAsciiField(totalSignals.ToString(CultureInfo.InvariantCulture),
            EdfConstants.SignalCountFieldSize);

        // --- Per-signal header ---

        // Labels (16 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.Label, EdfConstants.LabelFieldSize);

        // Transducer types (80 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.TransducerType, EdfConstants.TransducerTypeFieldSize);

        // Physical dimensions (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.PhysicalDimension, EdfConstants.PhysicalDimensionFieldSize);

        // Physical minimums (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.PhysicalMinimum.ToString(CultureInfo.InvariantCulture),
                EdfConstants.PhysicalMinFieldSize);

        // Physical maximums (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.PhysicalMaximum.ToString(CultureInfo.InvariantCulture),
                EdfConstants.PhysicalMaxFieldSize);

        // Digital minimums (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.DigitalMinimum.ToString(CultureInfo.InvariantCulture),
                EdfConstants.DigitalMinFieldSize);

        // Digital maximums (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.DigitalMaximum.ToString(CultureInfo.InvariantCulture),
                EdfConstants.DigitalMaxFieldSize);

        // Prefiltering (80 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.Prefiltering, EdfConstants.PrefilteringFieldSize);

        // Samples per data record (8 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.SamplesPerDataRecord.ToString(CultureInfo.InvariantCulture),
                EdfConstants.SamplesPerRecordFieldSize);

        // Reserved (32 bytes each)
        foreach (var sig in allSignals)
            WriteAsciiField(sig.Reserved, EdfConstants.SignalReservedFieldSize);
    }

    // -----------------------------------------------------------------------
    // Sample writing helpers
    // -----------------------------------------------------------------------

    private void WriteSample(int digital, int sampleSize)
    {
        if (sampleSize == EdfConstants.BdfSampleSizeBytes)
        {
            // 24-bit little-endian
            _stream.WriteByte((byte)(digital & 0xFF));
            _stream.WriteByte((byte)((digital >> 8) & 0xFF));
            _stream.WriteByte((byte)((digital >> 16) & 0xFF));
        }
        else
        {
            // 16-bit little-endian
            _stream.WriteByte((byte)(digital & 0xFF));
            _stream.WriteByte((byte)((digital >> 8) & 0xFF));
        }
    }

    /// <summary>
    /// Returns the file signal index for the current write position.
    /// If we have an annotation channel, it is handled automatically after all ordinary signals.
    /// </summary>
    private int GetCurrentWriteSignalIndex()
    {
        // User writes signals 0..(_signalCount-1). The annotation channel is automatic.
        return _currentSampleIndex;
    }

    private EdfSignalParameter GetWriteSignalParameter(int userSignalIndex)
    {
        if (userSignalIndex < _signalCount)
            return _signals[userSignalIndex];

        throw new InvalidOperationException("Cannot manually write to the annotation channel.");
    }

    /// <summary>
    /// Advances the signal cursor. After all ordinary signals are written,
    /// writes the annotation channel (if present), completing one data record.
    /// </summary>
    private void AdvanceSignalCursor()
    {
        _currentSampleIndex++;

        if (_currentSampleIndex >= _signalCount)
        {
            // All ordinary signals have been written for this data record.
            // Write the annotation channel if present.
            if (_hasAnnotationChannel)
            {
                WriteAnnotationChannel();
            }

            _dataRecordCount++;
            _currentSampleIndex = 0;
        }
    }

    /// <summary>
    /// Writes the annotation channel data for the current data record.
    /// Encodes pending annotations as TALs.
    /// </summary>
    private void WriteAnnotationChannel()
    {
        if (_annotationSignalIndex < 0) return;

        int sampleSize = IsBdf ? EdfConstants.BdfSampleSizeBytes : EdfConstants.EdfSampleSizeBytes;
        // Determine annotation signal samples per data record
        // We need to compute this from the header that was written
        int annSamplesPerRecord = 30; // default set during header writing
        int totalBytes = annSamplesPerRecord * sampleSize;

        // Build TAL for this data record
        var talBuilder = new List<byte>();

        // Time-keeping annotation (onset of this data record)
        double recordOnset = (_dataRecordCount) * _dataRecordDuration;
        string onsetStr = "+" + recordOnset.ToString("F7", CultureInfo.InvariantCulture);
        talBuilder.AddRange(Encoding.ASCII.GetBytes(onsetStr));
        talBuilder.Add(EdfConstants.TalFieldSeparator);
        talBuilder.Add(EdfConstants.TalFieldSeparator);
        talBuilder.Add(EdfConstants.TalTerminator);

        // Write any pending annotations that fall within or are due
        var written = new List<EdfAnnotation>();
        foreach (var ann in _pendingAnnotations)
        {
            var talBytes = EncodeTal(ann);
            if (talBuilder.Count + talBytes.Length <= totalBytes)
            {
                talBuilder.AddRange(talBytes);
                written.Add(ann);
            }
        }

        foreach (var ann in written)
            _pendingAnnotations.Remove(ann);

        // Pad to fill the annotation channel
        while (talBuilder.Count < totalBytes)
            talBuilder.Add(0x00);

        // Write exactly totalBytes
        byte[] annData = talBuilder.Count > totalBytes
            ? talBuilder.GetRange(0, totalBytes).ToArray()
            : talBuilder.ToArray();

        _stream.Write(annData, 0, totalBytes);
    }

    /// <summary>
    /// Encodes a single annotation as a TAL.
    /// </summary>
    private static byte[] EncodeTal(EdfAnnotation annotation)
    {
        var tal = new List<byte>();

        // Onset
        string onsetStr = annotation.Onset.Ticks >= 0
            ? "+" + annotation.Onset.TotalSeconds.ToString("F7", CultureInfo.InvariantCulture)
            : annotation.Onset.TotalSeconds.ToString("F7", CultureInfo.InvariantCulture);
        tal.AddRange(Encoding.ASCII.GetBytes(onsetStr));

        // Duration (optional)
        if (annotation.Duration.HasValue)
        {
            tal.Add(EdfConstants.TalDurationSeparator);
            string durStr = annotation.Duration.Value.TotalSeconds.ToString("F7", CultureInfo.InvariantCulture);
            tal.AddRange(Encoding.ASCII.GetBytes(durStr));
        }

        tal.Add(EdfConstants.TalFieldSeparator);

        // Description
        tal.AddRange(Encoding.UTF8.GetBytes(annotation.Description));
        tal.Add(EdfConstants.TalFieldSeparator);
        tal.Add(EdfConstants.TalTerminator);

        return tal.ToArray();
    }

    // -----------------------------------------------------------------------
    // Low-level helpers
    // -----------------------------------------------------------------------

    private void WriteAsciiField(string value, int fieldSize)
    {
        byte[] bytes = new byte[fieldSize];
        // Fill with spaces (ASCII 0x20)
        for (int i = 0; i < fieldSize; i++)
            bytes[i] = 0x20;

        byte[] encoded = Latin1.GetBytes(value ?? "");
        int copyLen = Math.Min(encoded.Length, fieldSize);
        Array.Copy(encoded, bytes, copyLen);

        _stream.Write(bytes, 0, fieldSize);
    }

    private void ValidateSignalIndex(int signal)
    {
        if (signal < 0 || signal >= _signalCount)
            throw new ArgumentOutOfRangeException(nameof(signal),
                $"Signal index {signal} is out of range. Valid range: 0..{_signalCount - 1}.");
    }

    private void ThrowIfHeaderWritten()
    {
        if (_headerWritten)
            throw new InvalidOperationException(
                "Cannot modify signal parameters after the header has been written. " +
                "Configure all parameters before the first write operation.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
