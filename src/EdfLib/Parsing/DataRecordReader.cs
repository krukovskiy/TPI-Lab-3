using EdfLib.Core;

namespace EdfLib.Parsing;

/// <summary>
/// Reads data records from an EDF/BDF file stream, converting between digital
/// (raw integer) and physical (calibrated double) sample values.
/// </summary>
public sealed class DataRecordReader
{
    private readonly Stream _stream;
    private readonly EdfHeader _header;
    private readonly long _dataStartOffset;
    private readonly int _dataRecordSize;
    private readonly int[] _signalByteOffsets; // byte offset of each signal within a data record

    /// <summary>
    /// Creates a new <see cref="DataRecordReader"/>.
    /// </summary>
    /// <param name="stream">The file stream, positioned anywhere (seeking is used).</param>
    /// <param name="header">The parsed EDF/BDF header.</param>
    public DataRecordReader(Stream stream, EdfHeader header)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);

        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(stream));

        _stream = stream;
        _header = header;
        _dataStartOffset = header.HeaderSizeInBytes;

        int sampleSize = header.SampleSizeInBytes;

        // Compute per-signal byte offsets within a data record
        _signalByteOffsets = new int[header.SignalCount];
        int offset = 0;
        for (int i = 0; i < header.SignalCount; i++)
        {
            _signalByteOffsets[i] = offset;
            offset += header.Signals[i].SamplesPerDataRecord * sampleSize;
        }
        _dataRecordSize = offset;
    }

    /// <summary>The total byte size of one data record.</summary>
    public int DataRecordSize => _dataRecordSize;

    /// <summary>
    /// Seeks the stream to the beginning of the specified data record.
    /// </summary>
    /// <param name="recordIndex">Zero-based data record index.</param>
    public void SeekToDataRecord(int recordIndex)
    {
        if (recordIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(recordIndex), "Record index must be non-negative.");

        long position = _dataStartOffset + (long)recordIndex * _dataRecordSize;
        _stream.Seek(position, SeekOrigin.Begin);
    }

    /// <summary>
    /// Reads an entire data record from the current stream position into digital sample buffers.
    /// </summary>
    /// <param name="digitalBuffers">
    /// An array of int arrays, one per signal. Each array must have length >= SamplesPerDataRecord
    /// for that signal. May be <c>null</c> for signals that should be skipped.
    /// </param>
    /// <returns>The number of bytes read.</returns>
    public int ReadDataRecordDigital(int[]?[] digitalBuffers)
    {
        ArgumentNullException.ThrowIfNull(digitalBuffers);
        if (digitalBuffers.Length != _header.SignalCount)
            throw new ArgumentException(
                $"Expected {_header.SignalCount} buffers, got {digitalBuffers.Length}.",
                nameof(digitalBuffers));

        byte[] rawRecord = new byte[_dataRecordSize];
        int bytesRead = ReadExactly(_stream, rawRecord, 0, _dataRecordSize);

        if (bytesRead < _dataRecordSize)
            return bytesRead; // Partial record at end of file

        bool isBdf = _header.IsBdf;

        for (int sig = 0; sig < _header.SignalCount; sig++)
        {
            int[] ? buffer = digitalBuffers[sig];
            if (buffer is null) continue;

            int samples = _header.Signals[sig].SamplesPerDataRecord;
            int byteOffset = _signalByteOffsets[sig];

            if (isBdf)
            {
                DecodeBdfSamples(rawRecord, byteOffset, buffer, samples);
            }
            else
            {
                DecodeEdfSamples(rawRecord, byteOffset, buffer, samples);
            }
        }

        return bytesRead;
    }

    /// <summary>
    /// Reads an entire data record from the current stream position and converts to physical values.
    /// </summary>
    /// <param name="physicalBuffers">
    /// An array of double arrays, one per signal. Each array must have length >= SamplesPerDataRecord.
    /// May be <c>null</c> for signals that should be skipped.
    /// </param>
    /// <returns>The number of bytes read.</returns>
    public int ReadDataRecordPhysical(double[]?[] physicalBuffers)
    {
        ArgumentNullException.ThrowIfNull(physicalBuffers);
        if (physicalBuffers.Length != _header.SignalCount)
            throw new ArgumentException(
                $"Expected {_header.SignalCount} buffers, got {physicalBuffers.Length}.",
                nameof(physicalBuffers));

        byte[] rawRecord = new byte[_dataRecordSize];
        int bytesRead = ReadExactly(_stream, rawRecord, 0, _dataRecordSize);

        if (bytesRead < _dataRecordSize)
            return bytesRead;

        bool isBdf = _header.IsBdf;

        for (int sig = 0; sig < _header.SignalCount; sig++)
        {
            double[]? buffer = physicalBuffers[sig];
            if (buffer is null) continue;

            var param = _header.Signals[sig];
            int samples = param.SamplesPerDataRecord;
            int byteOffset = _signalByteOffsets[sig];

            double bitValue = param.BitValue;
            double offset = param.Offset;

            if (isBdf)
            {
                for (int s = 0; s < samples; s++)
                {
                    int digital = DecodeBdf24(rawRecord, byteOffset + s * 3);
                    buffer[s] = (digital + offset) * bitValue;
                }
            }
            else
            {
                for (int s = 0; s < samples; s++)
                {
                    int digital = DecodeEdf16(rawRecord, byteOffset + s * 2);
                    buffer[s] = (digital + offset) * bitValue;
                }
            }
        }

        return bytesRead;
    }

    /// <summary>
    /// Reads the raw bytes of an annotation signal from a specific data record.
    /// </summary>
    /// <param name="recordIndex">Zero-based data record index.</param>
    /// <param name="annotationSignalIndex">
    /// The signal index (within the header's signal list) of the annotation channel.
    /// </param>
    /// <returns>The raw bytes of the annotation signal for that record.</returns>
    public byte[] ReadAnnotationSignalBytes(int recordIndex, int annotationSignalIndex)
    {
        if (annotationSignalIndex < 0 || annotationSignalIndex >= _header.SignalCount)
            throw new ArgumentOutOfRangeException(nameof(annotationSignalIndex));

        int samples = _header.Signals[annotationSignalIndex].SamplesPerDataRecord;
        int byteCount = samples * _header.SampleSizeInBytes;

        SeekToDataRecord(recordIndex);

        // Seek to the annotation signal's offset within the data record
        long signalPosition = _stream.Position + _signalByteOffsets[annotationSignalIndex];
        _stream.Seek(signalPosition, SeekOrigin.Begin);

        byte[] buffer = new byte[byteCount];
        ReadExactly(_stream, buffer, 0, byteCount);
        return buffer;
    }

    /// <summary>
    /// Returns the byte offset of a specific signal within a data record.
    /// </summary>
    public int GetSignalByteOffset(int signalIndex) => _signalByteOffsets[signalIndex];

    // -----------------------------------------------------------------------
    // Sample decoding
    // -----------------------------------------------------------------------

    /// <summary>Decode a 16-bit little-endian signed integer (EDF sample).</summary>
    private static int DecodeEdf16(byte[] data, int offset) =>
        (short)(data[offset] | (data[offset + 1] << 8));

    /// <summary>Decode a 24-bit little-endian signed integer (BDF sample).</summary>
    private static int DecodeBdf24(byte[] data, int offset)
    {
        int value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        // Sign extension from 24-bit to 32-bit
        if ((value & 0x800000) != 0)
            value |= unchecked((int)0xFF000000);
        return value;
    }

    private static void DecodeEdfSamples(byte[] raw, int byteOffset, int[] buffer, int sampleCount)
    {
        for (int s = 0; s < sampleCount; s++)
        {
            buffer[s] = DecodeEdf16(raw, byteOffset + s * 2);
        }
    }

    private static void DecodeBdfSamples(byte[] raw, int byteOffset, int[] buffer, int sampleCount)
    {
        for (int s = 0; s < sampleCount; s++)
        {
            buffer[s] = DecodeBdf24(raw, byteOffset + s * 3);
        }
    }

    // -----------------------------------------------------------------------
    // Stream helpers
    // -----------------------------------------------------------------------

    private static int ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                return totalRead; // EOF reached
            totalRead += read;
        }
        return totalRead;
    }
}
