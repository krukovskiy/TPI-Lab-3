namespace EdfBrowser.App.Services;

using System.Net;
using System.Net.Sockets;
using System.Text;

public interface IRemoteCommandService : IDisposable
{
    void Start(int port);
    void Stop();
    bool IsRunning { get; }
    event Action<string>? CommandReceived;
}

public sealed class RemoteCommandService : IRemoteCommandService
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public bool IsRunning => _listener != null;
    public event Action<string>? CommandReceived;

    public void Start(int port)
    {
        if (_listener != null) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        CommandReceived?.Invoke(trimmed);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
