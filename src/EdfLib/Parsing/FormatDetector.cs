using System.Text;
using EdfLib.Core;

namespace EdfLib.Parsing;

/// <summary>
/// Detects the EDF/BDF file format variant from the initial bytes of a file.
/// </summary>
public static class FormatDetector
{
    /// <summary>
    /// Detects the file format by reading the header from the given stream.
    /// The stream is rewound to its original position after detection.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the file.</param>
    /// <returns>The detected <see cref="EdfFileFormat"/>.</returns>
    /// <exception cref="InvalidDataException">The stream does not contain a valid EDF or BDF header.</exception>
    public static EdfFileFormat Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long originalPosition = stream.Position;

        try
        {
            // We need at least the fixed header to detect the format fully
            // but the first 8 bytes (version) + reserved (at offset 192, 44 bytes) are sufficient.
            // Read the entire fixed header (256 bytes) for simplicity.
            byte[] header = new byte[EdfConstants.FixedHeaderSize];
            int totalRead = 0;
            while (totalRead < header.Length)
            {
                int read = stream.Read(header, totalRead, header.Length - totalRead);
                if (read == 0)
                    throw new InvalidDataException(
                        $"File too short to contain an EDF/BDF header: only {totalRead} bytes available.");
                totalRead += read;
            }

            return DetectFromHeaderBytes(header);
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Detects the file format from the raw bytes of the fixed 256-byte header.
    /// </summary>
    /// <param name="headerBytes">At least 256 bytes of the file header.</param>
    /// <returns>The detected <see cref="EdfFileFormat"/>.</returns>
    /// <exception cref="InvalidDataException">The bytes do not match any known format.</exception>
    public static EdfFileFormat DetectFromHeaderBytes(ReadOnlySpan<byte> headerBytes)
    {
        if (headerBytes.Length < EdfConstants.FixedHeaderSize)
            throw new ArgumentException(
                $"Header bytes must be at least {EdfConstants.FixedHeaderSize} bytes.",
                nameof(headerBytes));

        bool isBdf = headerBytes[0] == EdfConstants.BdfVersionByte;

        if (!isBdf)
        {
            // Check for EDF: version field should start with "0"
            string version = Encoding.Latin1.GetString(headerBytes[..EdfConstants.VersionFieldSize]).TrimEnd();
            if (version != "0")
                throw new InvalidDataException(
                    $"Unrecognized EDF version field: '{version}'. Expected '0' or BDF marker 0xFF.");
        }
        else
        {
            // Validate BDF: bytes 1-7 should read "BIOSEMI"
            string biosemi = Encoding.Latin1.GetString(headerBytes.Slice(1, 7)).TrimEnd();
            if (!biosemi.StartsWith("BIOSEMI", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"BDF version marker found (0xFF) but expected 'BIOSEMI', got '{biosemi}'.");
        }

        // Read the reserved field (44 bytes starting at offset 192)
        int reservedOffset = EdfConstants.VersionFieldSize
            + EdfConstants.PatientIdFieldSize
            + EdfConstants.RecordingIdFieldSize
            + EdfConstants.StartDateFieldSize
            + EdfConstants.StartTimeFieldSize
            + EdfConstants.HeaderBytesFieldSize;

        string reserved = Encoding.Latin1.GetString(
            headerBytes.Slice(reservedOffset, EdfConstants.ReservedFieldSize)).TrimEnd();

        if (isBdf)
        {
            if (reserved.StartsWith(EdfConstants.BdfPlusContinuous, StringComparison.Ordinal) ||
                reserved.StartsWith(EdfConstants.BdfPlusDiscontinuous, StringComparison.Ordinal))
            {
                return EdfFileFormat.BdfPlus;
            }
            return EdfFileFormat.Bdf;
        }
        else
        {
            if (reserved.StartsWith(EdfConstants.EdfPlusContinuous, StringComparison.Ordinal) ||
                reserved.StartsWith(EdfConstants.EdfPlusDiscontinuous, StringComparison.Ordinal))
            {
                return EdfFileFormat.EdfPlus;
            }
            return EdfFileFormat.Edf;
        }
    }

    /// <summary>
    /// Quick check: is the first byte of the file a BDF marker (0xFF)?
    /// </summary>
    public static bool IsBdf(byte firstByte) => firstByte == EdfConstants.BdfVersionByte;
}
