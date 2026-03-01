using EdfLib.Parsing;

namespace EdfLib.Core;

/// <summary>
/// Main read API for EDF/BDF files. Wraps <see cref="HeaderParser"/>,
/// <see cref="AnnotationParser"/>, and <see cref="DataRecordReader"/>
/// into a convenient, per-signal sample-reading interface.
/// </summary>
public sealed class EdfReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly EdfHeader _header;
    private readonly List<EdfAnnotation> _annotations;
    private readonly long _dataStartOffset;
    private readonly long[] _samplePositions; // per-signal read cursor (sample index)
    private bool _disposed;

    // Maps user signal index -> file signal index (skipping annotation channels)
    private readonly int[] _userToFileSignalMap;

    /// <summary>
    /// Opens an EDF/BDF file from a file path.
    /// </summary>
    /// <param name="filePath">Path to the EDF/BDF file.</param>
    public EdfReader(string filePath)
        : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), leaveOpen: false)
    {
    }

    /// <summary>
    /// Opens an EDF/BDF file from a stream.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the file.</param>
    /// <param name="leaveOpen">If <c>true</c>, the stream is not disposed when this reader is disposed.</param>
    public EdfReader(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable.", nameof(stream));

        _stream = stream;
        _leaveOpen = leaveOpen;

        // Parse header
        _stream.Position = 0;
        _header = HeaderParser.Parse(_stream);

        // Detect format (confirms what HeaderParser already set)
        _stream.Position = 0;
        _ = FormatDetector.Detect(_stream);

        _dataStartOffset = _header.HeaderSizeInBytes;

        // Build user-to-file signal index map (skipping annotation channels)
        var signalMap = new List<int>();
        for (int i = 0; i < _header.Signals.Count; i++)
        {
            if (!_header.Signals[i].IsAnnotationSignal)
                signalMap.Add(i);
        }
        _userToFileSignalMap = signalMap.ToArray();

        // Initialize per-signal sample positions
        _samplePositions = new long[_userToFileSignalMap.Length];

        // Read all annotations from annotation channels
        _annotations = ReadAllAnnotations();
    }

    /// <summary>The parsed file header.</summary>
    public EdfHeader Header => _header;

    /// <summary>All annotations found in the file.</summary>
    public IReadOnlyList<EdfAnnotation> Annotations => _annotations;

    // -----------------------------------------------------------------------
    // Physical sample reading
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads physical (calibrated) samples for the given signal.
    /// Physical value = (digital + offset) * bitValue.
    /// </summary>
    /// <param name="signalIndex">User signal index (annotation channels are skipped).</param>
    /// <param name="buffer">Buffer to receive the samples.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>The number of samples actually read.</returns>
    public int ReadPhysicalSamples(int signalIndex, double[] buffer, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count < 0 || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        return ReadPhysicalSamples(signalIndex, buffer.AsSpan(0, count));
    }

    /// <summary>
    /// Reads physical (calibrated) samples for the given signal.
    /// </summary>
    /// <param name="signalIndex">User signal index (annotation channels are skipped).</param>
    /// <param name="buffer">Span to receive the samples.</param>
    /// <returns>The number of samples actually read.</returns>
    public int ReadPhysicalSamples(int signalIndex, Span<double> buffer)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);

        int fileSignalIndex = _userToFileSignalMap[signalIndex];
        var param = _header.Signals[fileSignalIndex];
        double bitValue = param.BitValue;
        double offset = param.Offset;

        int totalRead = 0;
        int remaining = buffer.Length;

        while (remaining > 0)
        {
            int digital = ReadNextDigitalSample(signalIndex, fileSignalIndex);
            if (digital == int.MinValue) // sentinel: no more data
                break;

            buffer[totalRead] = (digital + offset) * bitValue;
            totalRead++;
            remaining--;
        }

        return totalRead;
    }

    // -----------------------------------------------------------------------
    // Digital sample reading
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads raw digital (integer) samples for the given signal.
    /// </summary>
    /// <param name="signalIndex">User signal index (annotation channels are skipped).</param>
    /// <param name="buffer">Buffer to receive the samples.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>The number of samples actually read.</returns>
    public int ReadDigitalSamples(int signalIndex, int[] buffer, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count < 0 || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        return ReadDigitalSamples(signalIndex, buffer.AsSpan(0, count));
    }

    /// <summary>
    /// Reads raw digital (integer) samples for the given signal.
    /// </summary>
    /// <param name="signalIndex">User signal index (annotation channels are skipped).</param>
    /// <param name="buffer">Span to receive the samples.</param>
    /// <returns>The number of samples actually read.</returns>
    public int ReadDigitalSamples(int signalIndex, Span<int> buffer)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);

        int fileSignalIndex = _userToFileSignalMap[signalIndex];

        int totalRead = 0;
        int remaining = buffer.Length;

        while (remaining > 0)
        {
            int digital = ReadNextDigitalSample(signalIndex, fileSignalIndex);
            if (digital == int.MinValue)
                break;

            buffer[totalRead] = digital;
            totalRead++;
            remaining--;
        }

        return totalRead;
    }

    // -----------------------------------------------------------------------
    // Seeking
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeks the per-signal sample cursor to a given position.
    /// </summary>
    /// <param name="signalIndex">User signal index.</param>
    /// <param name="offset">Sample offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The reference point for the offset.</param>
    /// <returns>The new absolute sample position.</returns>
    public long Seek(int signalIndex, long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);

        long totalSamples = GetTotalSamples(signalIndex);
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _samplePositions[signalIndex] + offset,
            SeekOrigin.End => totalSamples + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0)
            newPosition = 0;
        if (newPosition > totalSamples)
            newPosition = totalSamples;

        _samplePositions[signalIndex] = newPosition;
        return newPosition;
    }

    /// <summary>
    /// Returns the current per-signal sample cursor position.
    /// </summary>
    /// <param name="signalIndex">User signal index.</param>
    /// <returns>The current sample position.</returns>
    public long Tell(int signalIndex)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);
        return _samplePositions[signalIndex];
    }

    /// <summary>
    /// Resets the per-signal sample cursor to the beginning.
    /// </summary>
    /// <param name="signalIndex">User signal index.</param>
    public void Rewind(int signalIndex)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);
        _samplePositions[signalIndex] = 0;
    }

    /// <summary>
    /// Returns the total number of samples available for a signal.
    /// </summary>
    /// <param name="signalIndex">User signal index.</param>
    /// <returns>Total sample count across all data records.</returns>
    public long GetTotalSamples(int signalIndex)
    {
        ThrowIfDisposed();
        ValidateUserSignalIndex(signalIndex);

        int fileSignalIndex = _userToFileSignalMap[signalIndex];
        long recordCount = _header.DataRecordCount;
        if (recordCount <= 0) return 0;

        return recordCount * _header.Signals[fileSignalIndex].SamplesPerDataRecord;
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    /// <summary>
    /// Disposes the reader and, unless <c>leaveOpen</c> was set, the underlying stream.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_leaveOpen)
            _stream.Dispose();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the next digital sample for a given signal at its current cursor position.
    /// Returns <see cref="int.MinValue"/> as a sentinel if no more data is available.
    /// </summary>
    private int ReadNextDigitalSample(int userSignalIndex, int fileSignalIndex)
    {
        long samplePos = _samplePositions[userSignalIndex];
        long totalSamples = GetTotalSamplesInternal(fileSignalIndex);
        if (samplePos >= totalSamples)
            return int.MinValue;

        var param = _header.Signals[fileSignalIndex];
        int samplesPerRecord = param.SamplesPerDataRecord;

        // Determine which data record and offset within that record
        long dataRecord = samplePos / samplesPerRecord;
        int sampleOffset = (int)(samplePos % samplesPerRecord);

        // Compute byte position in the stream
        int sampleSize = _header.SampleSizeInBytes;
        int dataRecordSize = _header.DataRecordSizeInBytes;

        // Byte offset of this signal within a data record
        int signalByteOffset = 0;
        for (int i = 0; i < fileSignalIndex; i++)
            signalByteOffset += _header.Signals[i].SamplesPerDataRecord * sampleSize;

        long bytePosition = _dataStartOffset
            + dataRecord * dataRecordSize
            + signalByteOffset
            + sampleOffset * sampleSize;

        _stream.Position = bytePosition;

        int digital;
        if (_header.IsBdf)
        {
            // 24-bit little-endian, sign-extended
            Span<byte> buf = stackalloc byte[3];
            if (ReadExactlyFromStream(buf) < 3)
                return int.MinValue;

            digital = buf[0] | (buf[1] << 8) | (buf[2] << 16);
            if ((digital & 0x800000) != 0)
                digital |= unchecked((int)0xFF000000);
        }
        else
        {
            // 16-bit little-endian
            Span<byte> buf = stackalloc byte[2];
            if (ReadExactlyFromStream(buf) < 2)
                return int.MinValue;

            digital = (short)(buf[0] | (buf[1] << 8));
        }

        _samplePositions[userSignalIndex] = samplePos + 1;
        return digital;
    }

    /// <summary>
    /// Reads all annotations from all data records' annotation channels.
    /// </summary>
    private List<EdfAnnotation> ReadAllAnnotations()
    {
        var annotations = new List<EdfAnnotation>();

        if (_header.DataRecordCount <= 0)
            return annotations;

        // Find annotation signal indices
        var annotationSignalIndices = new List<int>();
        for (int i = 0; i < _header.Signals.Count; i++)
        {
            if (_header.Signals[i].IsAnnotationSignal)
                annotationSignalIndices.Add(i);
        }

        if (annotationSignalIndices.Count == 0)
            return annotations;

        var dataRecordReader = new DataRecordReader(_stream, _header);

        for (long rec = 0; rec < _header.DataRecordCount; rec++)
        {
            foreach (int annSigIdx in annotationSignalIndices)
            {
                byte[] annBytes = dataRecordReader.ReadAnnotationSignalBytes((int)rec, annSigIdx);
                var parsed = AnnotationParser.ParseAnnotations(annBytes, annBytes.Length);
                annotations.AddRange(parsed);
            }
        }

        return annotations;
    }

    private long GetTotalSamplesInternal(int fileSignalIndex)
    {
        long recordCount = _header.DataRecordCount;
        if (recordCount <= 0) return 0;
        return recordCount * _header.Signals[fileSignalIndex].SamplesPerDataRecord;
    }

    private void ValidateUserSignalIndex(int signalIndex)
    {
        if (signalIndex < 0 || signalIndex >= _userToFileSignalMap.Length)
            throw new ArgumentOutOfRangeException(
                nameof(signalIndex),
                $"Signal index {signalIndex} is out of range. Valid range: 0..{_userToFileSignalMap.Length - 1}.");
    }

    private int ReadExactlyFromStream(Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            // Use array-based Read for compatibility
            byte[] temp = new byte[buffer.Length - totalRead];
            int read = _stream.Read(temp, 0, temp.Length);
            if (read == 0)
                return totalRead;
            temp.AsSpan(0, read).CopyTo(buffer[totalRead..]);
            totalRead += read;
        }
        return totalRead;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
