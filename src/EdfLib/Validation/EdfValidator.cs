using EdfLib.Core;
using EdfLib.Parsing;

namespace EdfLib.Validation;

/// <summary>
/// Validates EDF/BDF headers and files for correctness and consistency.
/// </summary>
public sealed class EdfValidator
{
    /// <summary>
    /// Validates an <see cref="EdfHeader"/> for structural and logical errors.
    /// </summary>
    /// <param name="header">The header to validate.</param>
    /// <returns>A list of validation errors (empty if the header is valid).</returns>
    public List<ValidationError> Validate(EdfHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var errors = new List<ValidationError>();

        // --- Signal count ---
        if (header.SignalCount <= 0)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = "Signal count must be greater than zero.",
                Field = nameof(header.SignalCount),
            });
        }

        if (header.SignalCount > EdfConstants.MaxSignals)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"Signal count {header.SignalCount} exceeds maximum of {EdfConstants.MaxSignals}.",
                Field = nameof(header.SignalCount),
            });
        }

        // --- Signal count vs Signals list consistency ---
        if (header.Signals.Count != header.SignalCount)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"SignalCount ({header.SignalCount}) does not match the number of signal parameters ({header.Signals.Count}).",
                Field = nameof(header.SignalCount),
            });
        }

        // --- Data record duration ---
        if (header.DataRecordDuration <= 0.0)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = "Data record duration must be greater than zero.",
                Field = nameof(header.DataRecordDuration),
            });
        }

        // --- Data record count ---
        if (header.DataRecordCount < -1)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"Data record count {header.DataRecordCount} is invalid. Must be >= -1.",
                Field = nameof(header.DataRecordCount),
            });
        }

        if (header.DataRecordCount == 0)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = "Data record count is zero. The file contains no data.",
                Field = nameof(header.DataRecordCount),
            });
        }

        // --- Header size consistency ---
        int expectedHeaderSize = EdfConstants.FixedHeaderSize + EdfConstants.SignalHeaderSize * header.SignalCount;
        if (header.HeaderSizeInBytes != expectedHeaderSize)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"Header size {header.HeaderSizeInBytes} does not match expected size {expectedHeaderSize} " +
                          $"(256 + 256 * {header.SignalCount}).",
                Field = nameof(header.HeaderSizeInBytes),
            });
        }

        // --- Start date/time ---
        if (header.StartDateTime == default)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = "Start date/time is not set (default value).",
                Field = nameof(header.StartDateTime),
            });
        }

        if (header.StartDateTime.Year < 1985 || header.StartDateTime.Year > 2084)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"Start year {header.StartDateTime.Year} is outside the EDF representable range (1985-2084).",
                Field = nameof(header.StartDateTime),
            });
        }

        // --- Per-signal validation ---
        for (int i = 0; i < header.Signals.Count; i++)
        {
            ValidateSignal(header, i, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validates a file by parsing its header and checking both structural and data integrity.
    /// </summary>
    /// <param name="filePath">Path to the EDF/BDF file.</param>
    /// <returns>A list of validation errors.</returns>
    public List<ValidationError> ValidateFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var errors = new List<ValidationError>();

        if (!File.Exists(filePath))
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"File not found: {filePath}",
                Field = "FilePath",
            });
            return errors;
        }

        EdfHeader header;
        long fileLength;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileLength = stream.Length;

            if (fileLength < EdfConstants.FixedHeaderSize)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"File is too small ({fileLength} bytes) to contain an EDF/BDF header ({EdfConstants.FixedHeaderSize} bytes).",
                    Field = "FileSize",
                });
                return errors;
            }

            try
            {
                header = HeaderParser.Parse(stream);
            }
            catch (InvalidDataException ex)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"Failed to parse header: {ex.Message}",
                    Field = "Header",
                });
                return errors;
            }
        }
        catch (IOException ex)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"Cannot read file: {ex.Message}",
                Field = "File",
            });
            return errors;
        }

        // Validate the parsed header
        errors.AddRange(Validate(header));

        // --- File size consistency ---
        if (header.DataRecordCount > 0)
        {
            long expectedDataBytes = header.DataRecordCount * header.DataRecordSizeInBytes;
            long expectedFileSize = header.HeaderSizeInBytes + expectedDataBytes;

            if (fileLength < expectedFileSize)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"File is truncated. Expected at least {expectedFileSize} bytes " +
                              $"(header: {header.HeaderSizeInBytes} + data: {expectedDataBytes}), " +
                              $"but file is only {fileLength} bytes.",
                    Field = "FileSize",
                });
            }
            else if (fileLength > expectedFileSize)
            {
                long extra = fileLength - expectedFileSize;
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"File contains {extra} extra bytes beyond the expected {expectedFileSize} bytes.",
                    Field = "FileSize",
                });
            }
        }

        return errors;
    }

    // -----------------------------------------------------------------------
    // Per-signal validation
    // -----------------------------------------------------------------------

    private static void ValidateSignal(EdfHeader header, int index, List<ValidationError> errors)
    {
        var signal = header.Signals[index];
        string prefix = $"Signal[{index}]";

        // --- Samples per data record ---
        if (signal.SamplesPerDataRecord <= 0)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"{prefix}: Samples per data record must be positive, got {signal.SamplesPerDataRecord}.",
                Field = $"{prefix}.{nameof(signal.SamplesPerDataRecord)}",
            });
        }

        // Skip digital/physical range checks for annotation channels
        if (signal.IsAnnotationSignal)
            return;

        // --- Digital range ---
        if (signal.DigitalMinimum >= signal.DigitalMaximum)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"{prefix}: Digital minimum ({signal.DigitalMinimum}) must be less than digital maximum ({signal.DigitalMaximum}).",
                Field = $"{prefix}.DigitalMinimum/DigitalMaximum",
            });
        }

        // Check digital range limits based on format
        if (header.IsBdf)
        {
            if (signal.DigitalMinimum < EdfConstants.BdfDigitalMin)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"{prefix}: Digital minimum ({signal.DigitalMinimum}) is below BDF limit ({EdfConstants.BdfDigitalMin}).",
                    Field = $"{prefix}.{nameof(signal.DigitalMinimum)}",
                });
            }

            if (signal.DigitalMaximum > EdfConstants.BdfDigitalMax)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"{prefix}: Digital maximum ({signal.DigitalMaximum}) exceeds BDF limit ({EdfConstants.BdfDigitalMax}).",
                    Field = $"{prefix}.{nameof(signal.DigitalMaximum)}",
                });
            }
        }
        else
        {
            if (signal.DigitalMinimum < EdfConstants.EdfDigitalMin)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"{prefix}: Digital minimum ({signal.DigitalMinimum}) is below EDF limit ({EdfConstants.EdfDigitalMin}).",
                    Field = $"{prefix}.{nameof(signal.DigitalMinimum)}",
                });
            }

            if (signal.DigitalMaximum > EdfConstants.EdfDigitalMax)
            {
                errors.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    Message = $"{prefix}: Digital maximum ({signal.DigitalMaximum}) exceeds EDF limit ({EdfConstants.EdfDigitalMax}).",
                    Field = $"{prefix}.{nameof(signal.DigitalMaximum)}",
                });
            }
        }

        // --- Physical range ---
        if (signal.PhysicalMinimum >= signal.PhysicalMaximum)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                Message = $"{prefix}: Physical minimum ({signal.PhysicalMinimum}) must be less than physical maximum ({signal.PhysicalMaximum}).",
                Field = $"{prefix}.PhysicalMinimum/PhysicalMaximum",
            });
        }

        // --- Label ---
        if (string.IsNullOrWhiteSpace(signal.Label))
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"{prefix}: Signal label is empty or whitespace.",
                Field = $"{prefix}.{nameof(signal.Label)}",
            });
        }

        if (signal.Label.Length > EdfConstants.LabelFieldSize)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"{prefix}: Signal label exceeds {EdfConstants.LabelFieldSize} characters and will be truncated.",
                Field = $"{prefix}.{nameof(signal.Label)}",
            });
        }

        // --- Physical dimension ---
        if (signal.PhysicalDimension.Length > EdfConstants.PhysicalDimensionFieldSize)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"{prefix}: Physical dimension exceeds {EdfConstants.PhysicalDimensionFieldSize} characters and will be truncated.",
                Field = $"{prefix}.{nameof(signal.PhysicalDimension)}",
            });
        }

        // --- Transducer type ---
        if (signal.TransducerType.Length > EdfConstants.TransducerTypeFieldSize)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"{prefix}: Transducer type exceeds {EdfConstants.TransducerTypeFieldSize} characters and will be truncated.",
                Field = $"{prefix}.{nameof(signal.TransducerType)}",
            });
        }

        // --- Prefiltering ---
        if (signal.Prefiltering.Length > EdfConstants.PrefilteringFieldSize)
        {
            errors.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                Message = $"{prefix}: Prefiltering exceeds {EdfConstants.PrefilteringFieldSize} characters and will be truncated.",
                Field = $"{prefix}.{nameof(signal.Prefiltering)}",
            });
        }
    }
}

/// <summary>
/// Represents a single validation error or warning found during header/file validation.
/// </summary>
public sealed class ValidationError
{
    /// <summary>The severity level of this validation finding.</summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>A human-readable description of the issue.</summary>
    public string Message { get; init; } = "";

    /// <summary>The header field or file property where the issue was found, or <c>null</c> if not field-specific.</summary>
    public string? Field { get; init; }

    public override string ToString() =>
        Field is not null
            ? $"[{Severity}] {Field}: {Message}"
            : $"[{Severity}] {Message}";
}

/// <summary>
/// Severity levels for validation findings.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>A non-critical issue that does not prevent the file from being read.</summary>
    Warning,

    /// <summary>A critical issue that indicates the file is malformed or corrupt.</summary>
    Error
}
