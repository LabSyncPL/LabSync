using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Modules.SSH.Interfaces;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LabSync.Modules.SSH.Services;

public class RemoteShellService : IRemoteShellService
{
    private readonly ILogger<RemoteShellService> _logger;
    private SshClient? _client;
    private ShellStream? _shellStream;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event EventHandler<string>? OutputReceived;

    public RemoteShellService(ILogger<RemoteShellService> logger)
    {
        _logger = logger;
    }

    public async Task OpenSessionAsync(string host, string username, string password, string terminalName = "xterm", uint columns = 80, uint rows = 24, CancellationToken cancellationToken = default)
    {
        if (_client != null && _client.IsConnected)
        {
            throw new InvalidOperationException("Session already open.");
        }

        _logger.LogInformation("Opening SSH session to {Host} with terminal {Terminal}", host, terminalName);

        _client = new SshClient(host, username, password);
        
        try
        {
            await _client.ConnectAsync(cancellationToken);
            _shellStream = _client.CreateShellStream(terminalName, columns, rows, 0, 0, 1024);
            _readCts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), cancellationToken);

            _logger.LogInformation("SSH Shell session established.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SSH session to {Host}", host);
            await DisposeAsync();
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        if (_shellStream == null) return;

        var buffer = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested && _shellStream != null)
            {
                int bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length, token);
                
                if (bytesRead == 0)
                {
                    break;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                OutputReceived?.Invoke(this, text);
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from SSH shell stream.");
        }
    }

    public async Task WriteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (_shellStream == null)
        {
            throw new InvalidOperationException("Shell session not open.");
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(input);
            await _shellStream.WriteAsync(data, 0, data.Length, cancellationToken);
            await _shellStream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to SSH shell.");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _readCts?.Cancel();
        
        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
               
            }
        }

        _readCts?.Dispose();
        _shellStream?.Dispose();
        _client?.Disconnect();
        _client?.Dispose();

        _client = null;
        _shellStream = null;
        _readCts = null;
        _readTask = null;

        GC.SuppressFinalize(this);
    }
}
