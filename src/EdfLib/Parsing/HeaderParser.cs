using System.Globalization;
using System.Text;
using EdfLib.Core;

namespace EdfLib.Parsing;

/// <summary>
/// Parses the fixed and per-signal header of EDF/BDF files from a stream.
/// </summary>
public static class HeaderParser
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private static readonly string[] MonthAbbreviations =
    [
        "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
        "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
    ];

    /// <summary>
    /// Parses the EDF/BDF header from the beginning of a stream.
    /// The stream position is advanced past the header on success.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the file.</param>
    /// <returns>A fully populated <see cref="EdfHeader"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the header data is malformed.</exception>
    public static EdfHeader Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        // Read the fixed 256-byte header
        byte[] fixedHeader = new byte[EdfConstants.FixedHeaderSize];
        ReadExactly(stream, fixedHeader, 0, fixedHeader.Length);

        var header = new EdfHeader();

        // --- Version (8 bytes) ---
        bool isBdf = fixedHeader[0] == EdfConstants.BdfVersionByte;
        if (isBdf)
        {
            header.Version = "BIOSEMI";
            header.Format = EdfFileFormat.Bdf;
        }
        else
        {
            header.Version = ReadField(fixedHeader, 0, EdfConstants.VersionFieldSize);
            header.Format = EdfFileFormat.Edf;
        }

        // --- Patient ID (80 bytes) ---
        int offset = EdfConstants.VersionFieldSize;
        header.PatientId = ReadField(fixedHeader, offset, EdfConstants.PatientIdFieldSize);
        offset += EdfConstants.PatientIdFieldSize;

        // --- Recording ID (80 bytes) ---
        header.RecordingId = ReadField(fixedHeader, offset, EdfConstants.RecordingIdFieldSize);
        offset += EdfConstants.RecordingIdFieldSize;

        // --- Start date (8 bytes: dd.mm.yy) ---
        string startDateStr = ReadField(fixedHeader, offset, EdfConstants.StartDateFieldSize);
        offset += EdfConstants.StartDateFieldSize;

        // --- Start time (8 bytes: hh.mm.ss) ---
        string startTimeStr = ReadField(fixedHeader, offset, EdfConstants.StartTimeFieldSize);
        offset += EdfConstants.StartTimeFieldSize;

        header.StartDateTime = ParseStartDateTime(startDateStr, startTimeStr);

        // --- Header size (8 bytes) ---
        string headerBytesStr = ReadField(fixedHeader, offset, EdfConstants.HeaderBytesFieldSize);
        offset += EdfConstants.HeaderBytesFieldSize;
        header.HeaderSizeInBytes = ParseInt(headerBytesStr, "header bytes");

        // --- Reserved (44 bytes) ---
        header.Reserved = ReadField(fixedHeader, offset, EdfConstants.ReservedFieldSize);
        offset += EdfConstants.ReservedFieldSize;

        // Detect EDF+/BDF+ from reserved field
        DetectPlusFormat(header);

        // --- Number of data records (8 bytes) ---
        string nrRecsStr = ReadField(fixedHeader, offset, EdfConstants.DataRecordCountFieldSize);
        offset += EdfConstants.DataRecordCountFieldSize;
        header.DataRecordCount = ParseLong(nrRecsStr, "data record count");

        // --- Data record duration (8 bytes) ---
        string durationStr = ReadField(fixedHeader, offset, EdfConstants.DataRecordDurationFieldSize);
        offset += EdfConstants.DataRecordDurationFieldSize;
        header.DataRecordDuration = ParseDouble(durationStr, "data record duration");

        // --- Number of signals (4 bytes) ---
        string nrSignalsStr = ReadField(fixedHeader, offset, EdfConstants.SignalCountFieldSize);
        header.SignalCount = ParseInt(nrSignalsStr, "signal count");

        if (header.SignalCount < 0 || header.SignalCount > EdfConstants.MaxSignals)
            throw new InvalidDataException(
                $"Signal count {header.SignalCount} is out of valid range [0..{EdfConstants.MaxSignals}].");

        // --- Read per-signal header (256 * nSignals bytes) ---
        int signalHeaderBytes = EdfConstants.SignalHeaderSize * header.SignalCount;
        byte[] signalHeader = new byte[signalHeaderBytes];
        ReadExactly(stream, signalHeader, 0, signalHeaderBytes);

        ParseSignalHeaders(header, signalHeader);

        // --- Parse structured EDF+ patient and recording fields ---
        if (header.Format is EdfFileFormat.EdfPlus or EdfFileFormat.BdfPlus)
        {
            ParseEdfPlusPatientId(header);
            ParseEdfPlusRecordingId(header);
        }

        return header;
    }

    // -----------------------------------------------------------------------
    // Signal header parsing
    // -----------------------------------------------------------------------

    private static void ParseSignalHeaders(EdfHeader header, byte[] data)
    {
        int ns = header.SignalCount;
        header.Signals = new List<EdfSignalParameter>(ns);

        for (int i = 0; i < ns; i++)
            header.Signals.Add(new EdfSignalParameter());

        int pos = 0;

        // Labels (16 bytes each)
        for (int i = 0; i < ns; i++)
        {
            header.Signals[i].Label = ReadField(data, pos, EdfConstants.LabelFieldSize);
            pos += EdfConstants.LabelFieldSize;
        }

        // Transducer types (80 bytes each)
        for (int i = 0; i < ns; i++)
        {
            header.Signals[i].TransducerType = ReadField(data, pos, EdfConstants.TransducerTypeFieldSize);
            pos += EdfConstants.TransducerTypeFieldSize;
        }

        // Physical dimensions (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            header.Signals[i].PhysicalDimension = ReadField(data, pos, EdfConstants.PhysicalDimensionFieldSize);
            pos += EdfConstants.PhysicalDimensionFieldSize;
        }

        // Physical minimums (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            string val = ReadField(data, pos, EdfConstants.PhysicalMinFieldSize);
            header.Signals[i].PhysicalMinimum = ParseDouble(val, $"signal {i} physical minimum");
            pos += EdfConstants.PhysicalMinFieldSize;
        }

        // Physical maximums (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            string val = ReadField(data, pos, EdfConstants.PhysicalMaxFieldSize);
            header.Signals[i].PhysicalMaximum = ParseDouble(val, $"signal {i} physical maximum");
            pos += EdfConstants.PhysicalMaxFieldSize;
        }

        // Digital minimums (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            string val = ReadField(data, pos, EdfConstants.DigitalMinFieldSize);
            header.Signals[i].DigitalMinimum = ParseInt(val, $"signal {i} digital minimum");
            pos += EdfConstants.DigitalMinFieldSize;
        }

        // Digital maximums (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            string val = ReadField(data, pos, EdfConstants.DigitalMaxFieldSize);
            header.Signals[i].DigitalMaximum = ParseInt(val, $"signal {i} digital maximum");
            pos += EdfConstants.DigitalMaxFieldSize;
        }

        // Prefiltering (80 bytes each)
        for (int i = 0; i < ns; i++)
        {
            header.Signals[i].Prefiltering = ReadField(data, pos, EdfConstants.PrefilteringFieldSize);
            pos += EdfConstants.PrefilteringFieldSize;
        }

        // Samples per data record (8 bytes each)
        for (int i = 0; i < ns; i++)
        {
            string val = ReadField(data, pos, EdfConstants.SamplesPerRecordFieldSize);
            header.Signals[i].SamplesPerDataRecord = ParseInt(val, $"signal {i} samples per record");
            pos += EdfConstants.SamplesPerRecordFieldSize;
        }

        // Reserved (32 bytes each)
        for (int i = 0; i < ns; i++)
        {
            header.Signals[i].Reserved = ReadField(data, pos, EdfConstants.SignalReservedFieldSize);
            pos += EdfConstants.SignalReservedFieldSize;
        }

        // Mark annotation channels
        for (int i = 0; i < ns; i++)
        {
            string trimmedLabel = header.Signals[i].Label.Trim();
            header.Signals[i].IsAnnotationSignal =
                trimmedLabel == EdfConstants.EdfAnnotationsLabel ||
                trimmedLabel == EdfConstants.BdfAnnotationsLabel;
        }
    }

    // -----------------------------------------------------------------------
    // EDF+ structured fields
    // -----------------------------------------------------------------------

    private static void ParseEdfPlusPatientId(EdfHeader header)
    {
        // Format: code sex birthdate name [additional sub-fields]
        // Sub-fields separated by spaces. 'X' means unknown/not specified.
        string[] parts = header.PatientId.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1)
            header.PatientCode = parts[0] == "X" ? string.Empty : parts[0];

        if (parts.Length >= 2)
            header.PatientSex = parts[1];

        if (parts.Length >= 3)
            header.PatientBirthDate = ParseEdfPlusDate(parts[2]);

        if (parts.Length >= 4)
            header.PatientName = parts[3] == "X" ? string.Empty : parts[3];
    }

    private static void ParseEdfPlusRecordingId(EdfHeader header)
    {
        // Format: Startdate DD-MMM-YYYY admincode technician equipment
        string[] parts = header.RecordingId.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 1 || !parts[0].Equals("Startdate", StringComparison.OrdinalIgnoreCase))
            return;

        if (parts.Length >= 2)
            header.RecordingStartDate = ParseEdfPlusDate(parts[1]);

        if (parts.Length >= 3)
            header.RecordingAdminCode = parts[2] == "X" ? string.Empty : parts[2];

        if (parts.Length >= 4)
            header.RecordingTechnician = parts[3] == "X" ? string.Empty : parts[3];

        if (parts.Length >= 5)
            header.RecordingEquipment = parts[4] == "X" ? string.Empty : parts[4];
    }

    /// <summary>
    /// Parse a date in EDF+ format: DD-MMM-YYYY (e.g. "02-AUG-1951").
    /// </summary>
    private static DateTime? ParseEdfPlusDate(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "X")
            return null;

        string[] parts = value.Split('-');
        if (parts.Length != 3) return null;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
            return null;

        int month = Array.FindIndex(MonthAbbreviations, m =>
            m.Equals(parts[1], StringComparison.OrdinalIgnoreCase)) + 1;
        if (month == 0) return null;

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
            return null;

        try
        {
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        }
        catch
        {
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Format detection
    // -----------------------------------------------------------------------

    private static void DetectPlusFormat(EdfHeader header)
    {
        string reserved = header.Reserved;

        if (reserved.StartsWith(EdfConstants.EdfPlusContinuous, StringComparison.Ordinal))
        {
            header.Format = EdfFileFormat.EdfPlus;
            header.IsContinuous = true;
        }
        else if (reserved.StartsWith(EdfConstants.EdfPlusDiscontinuous, StringComparison.Ordinal))
        {
            header.Format = EdfFileFormat.EdfPlus;
            header.IsContinuous = false;
        }
        else if (reserved.StartsWith(EdfConstants.BdfPlusContinuous, StringComparison.Ordinal))
        {
            header.Format = EdfFileFormat.BdfPlus;
            header.IsContinuous = true;
        }
        else if (reserved.StartsWith(EdfConstants.BdfPlusDiscontinuous, StringComparison.Ordinal))
        {
            header.Format = EdfFileFormat.BdfPlus;
            header.IsContinuous = false;
        }
    }

    // -----------------------------------------------------------------------
    // Date / time helpers
    // -----------------------------------------------------------------------

    private static DateTime ParseStartDateTime(string dateStr, string timeStr)
    {
        // Date format: dd.mm.yy
        // Time format: hh.mm.ss
        string[] dateParts = dateStr.Trim().Split('.');
        string[] timeParts = timeStr.Trim().Split('.');

        if (dateParts.Length != 3 || timeParts.Length != 3)
            throw new InvalidDataException(
                $"Invalid date/time format: date='{dateStr}', time='{timeStr}'.");

        if (!int.TryParse(dateParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day) ||
            !int.TryParse(dateParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) ||
            !int.TryParse(dateParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year2) ||
            !int.TryParse(timeParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hour) ||
            !int.TryParse(timeParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minute) ||
            !int.TryParse(timeParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int second))
        {
            throw new InvalidDataException(
                $"Invalid numeric values in date/time: date='{dateStr}', time='{timeStr}'.");
        }

        // EDF spec: yy < 85 => 20yy, yy >= 85 => 19yy
        int year = year2 >= 85 ? 1900 + year2 : 2000 + year2;

        try
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException(
                $"Date/time values out of range: date='{dateStr}', time='{timeStr}'.", ex);
        }
    }

    // -----------------------------------------------------------------------
    // Low-level helpers
    // -----------------------------------------------------------------------

    private static string ReadField(byte[] buffer, int offset, int length) =>
        Latin1.GetString(buffer, offset, length).TrimEnd();

    private static int ParseInt(string value, string fieldName)
    {
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            return result;
        throw new InvalidDataException($"Cannot parse '{value}' as integer for field '{fieldName}'.");
    }

    private static long ParseLong(string value, string fieldName)
    {
        if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            return result;
        throw new InvalidDataException($"Cannot parse '{value}' as long for field '{fieldName}'.");
    }

    private static double ParseDouble(string value, string fieldName)
    {
        if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            return result;
        throw new InvalidDataException($"Cannot parse '{value}' as double for field '{fieldName}'.");
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                throw new InvalidDataException(
                    $"Unexpected end of stream: expected {count} bytes, got {totalRead}.");
            totalRead += read;
        }
    }
}
