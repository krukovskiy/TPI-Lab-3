namespace EdfBrowser.App.Services;

using EdfBrowser.App.Models;
using EdfLib.Core;

public interface IFileManagerService
{
    IReadOnlyList<EdfFileModel?> OpenFiles { get; }
    int FileCount { get; }
    EdfFileModel OpenFile(string filePath);
    void CloseFile(int fileIndex);
    void CloseAllFiles();
    event Action<EdfFileModel>? FileOpened;
    event Action<int>? FileClosed;
}

public sealed class FileManagerService : IFileManagerService, IDisposable
{
    private const int MaxFiles = 32;
    private readonly EdfFileModel?[] _files = new EdfFileModel?[MaxFiles];
    private int _fileCount;
    private bool _disposed;

    public IReadOnlyList<EdfFileModel?> OpenFiles => _files;
    public int FileCount => _fileCount;

    public event Action<EdfFileModel>? FileOpened;
    public event Action<int>? FileClosed;

    public EdfFileModel OpenFile(string filePath)
    {
        if (_fileCount >= MaxFiles)
            throw new InvalidOperationException($"Cannot open more than {MaxFiles} files simultaneously.");

        int freeIndex = -1;
        for (int i = 0; i < MaxFiles; i++)
        {
            if (_files[i] == null)
            {
                freeIndex = i;
                break;
            }
        }

        if (freeIndex < 0)
            throw new InvalidOperationException("No free file slot available.");

        var model = new EdfFileModel(filePath) { FileIndex = freeIndex };
        _files[freeIndex] = model;
        _fileCount++;

        FileOpened?.Invoke(model);
        return model;
    }

    public void CloseFile(int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= MaxFiles)
            throw new ArgumentOutOfRangeException(nameof(fileIndex));

        var file = _files[fileIndex];
        if (file == null) return;

        file.Dispose();
        _files[fileIndex] = null;
        _fileCount--;

        FileClosed?.Invoke(fileIndex);
    }

    public void CloseAllFiles()
    {
        for (int i = 0; i < MaxFiles; i++)
        {
            if (_files[i] != null)
                CloseFile(i);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAllFiles();
    }
}
