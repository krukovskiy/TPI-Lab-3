using EdfLib.Core;

namespace EdfBrowser.App.Models;

/// <summary>
/// Represents an open EDF/BDF file with its reader and metadata.
/// </summary>
public sealed class EdfFileModel : IDisposable
{
    private bool _disposed;

    public EdfFileModel(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Reader = new EdfReader(filePath);
        Header = Reader.Header;
        DurationTicks = Header.Duration.Ticks;
    }

    /// <summary>Full path to the EDF/BDF file.</summary>
    public string FilePath { get; }

    /// <summary>File name only (no directory).</summary>
    public string FileName { get; }

    /// <summary>The EDF reader instance for reading samples.</summary>
    public EdfReader Reader { get; }

    /// <summary>The parsed file header.</summary>
    public EdfHeader Header { get; }

    /// <summary>Total file duration in 100-ns ticks.</summary>
    public long DurationTicks { get; }

    /// <summary>Index slot assigned to this file in the open files collection.</summary>
    public int FileIndex { get; set; }

    /// <summary>All annotations from the file.</summary>
    public IReadOnlyList<EdfAnnotation> Annotations => Reader.Annotations;

    /// <summary>
    /// Gets the ordinary (non-annotation) signals with their original indices.
    /// </summary>
    public IReadOnlyList<(int OriginalIndex, EdfSignalParameter Parameter)> OrdinarySignals
        => Header.GetOrdinarySignals();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reader.Dispose();
    }

    public override string ToString() => FileName;
}
