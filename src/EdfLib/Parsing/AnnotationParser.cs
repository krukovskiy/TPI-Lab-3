using System.Globalization;
using System.Text;
using EdfLib.Core;

namespace EdfLib.Parsing;

/// <summary>
/// Parses EDF+ / BDF+ Time-stamped Annotation Lists (TALs) from annotation signal data.
/// </summary>
/// <remarks>
/// TAL format:
/// <code>
///   +onset\x15duration\x14annotation1\x14annotation2\x14...\x00
///   +onset\x14annotation1\x14...\x00
/// </code>
/// - Onset: signed decimal seconds (ASCII), terminated by 0x15 (if duration follows) or 0x14.
/// - Duration: unsigned decimal seconds (ASCII), terminated by 0x14. Optional.
/// - Annotations: UTF-8 text, each terminated by 0x14.
/// - Each TAL is terminated by 0x00.
/// </remarks>
public static class AnnotationParser
{
    /// <summary>
    /// Parses all annotations from a byte buffer containing one or more TALs.
    /// </summary>
    /// <param name="data">The raw annotation signal data bytes.</param>
    /// <param name="length">Number of valid bytes in <paramref name="data"/>.</param>
    /// <returns>A list of parsed annotations.</returns>
    public static List<EdfAnnotation> ParseAnnotations(byte[] data, int length)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (length < 0 || length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        var annotations = new List<EdfAnnotation>();
        int pos = 0;

        while (pos < length)
        {
            // Skip any padding NUL bytes between TALs
            while (pos < length && data[pos] == EdfConstants.TalTerminator)
                pos++;

            if (pos >= length)
                break;

            // Parse one TAL
            ParseTal(data, length, ref pos, annotations);
        }

        return annotations;
    }

    /// <summary>
    /// Parses annotations from a complete data record's annotation signal buffer.
    /// </summary>
    /// <param name="data">The annotation signal buffer (span).</param>
    /// <returns>A list of parsed annotations.</returns>
    public static List<EdfAnnotation> ParseAnnotations(ReadOnlySpan<byte> data)
    {
        // Materialize to array for the main parser
        return ParseAnnotations(data.ToArray(), data.Length);
    }

    private static void ParseTal(byte[] data, int length, ref int pos, List<EdfAnnotation> annotations)
    {
        // --- Parse onset ---
        int onsetStart = pos;

        // The onset ends at 0x14 (no duration) or 0x15 (duration follows)
        while (pos < length && data[pos] != EdfConstants.TalFieldSeparator && data[pos] != EdfConstants.TalDurationSeparator)
            pos++;

        if (pos >= length)
            return; // Truncated TAL

        string onsetStr = Encoding.ASCII.GetString(data, onsetStart, pos - onsetStart);
        EdfTime onset = ParseTimeValue(onsetStr);

        EdfTime? duration = null;

        if (data[pos] == EdfConstants.TalDurationSeparator)
        {
            // Duration present
            pos++; // skip 0x15
            int durStart = pos;

            while (pos < length && data[pos] != EdfConstants.TalFieldSeparator)
                pos++;

            if (pos >= length)
                return; // Truncated TAL

            string durStr = Encoding.ASCII.GetString(data, durStart, pos - durStart);
            if (!string.IsNullOrEmpty(durStr))
                duration = ParseTimeValue(durStr);
        }

        // pos is now at the 0x14 after onset (or after duration)
        pos++; // skip the 0x14

        // --- Parse annotations within this TAL ---
        // Each annotation is UTF-8 text terminated by 0x14.
        // The TAL ends when we hit 0x00 or run out of data.
        while (pos < length && data[pos] != EdfConstants.TalTerminator)
        {
            int annStart = pos;

            while (pos < length && data[pos] != EdfConstants.TalFieldSeparator && data[pos] != EdfConstants.TalTerminator)
                pos++;

            if (pos > annStart)
            {
                string description = Encoding.UTF8.GetString(data, annStart, pos - annStart);

                if (!string.IsNullOrEmpty(description))
                {
                    // Truncate to max annotation length if needed
                    if (description.Length > EdfConstants.MaxAnnotationLength)
                        description = description[..EdfConstants.MaxAnnotationLength];

                    annotations.Add(new EdfAnnotation(onset, duration, description));
                }
            }

            // Skip the 0x14 separator
            if (pos < length && data[pos] == EdfConstants.TalFieldSeparator)
                pos++;
        }

        // Skip the 0x00 terminator
        if (pos < length && data[pos] == EdfConstants.TalTerminator)
            pos++;
    }

    /// <summary>
    /// Parses a TAL time value (onset or duration) as seconds into an <see cref="EdfTime"/>.
    /// Values may be signed (e.g. "+1.234", "-0.5") or unsigned ("1.234").
    /// </summary>
    private static EdfTime ParseTimeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return EdfTime.Zero;

        // The value is in seconds with optional fractional part
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out double seconds))
        {
            return EdfTime.FromSeconds(seconds);
        }

        // If parsing fails, return zero rather than throwing for robustness
        return EdfTime.Zero;
    }
}
