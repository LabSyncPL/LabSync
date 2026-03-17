using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using LabSync.Server.Hubs;
using Renci.SshNet;

namespace LabSync.Server.Services;

public class SshSessionManager : IAsyncDisposable
{
    private readonly ILogger<SshSessionManager> _logger;
    private readonly IHubContext<SshTerminalHub> _hubContext;
    private readonly ConcurrentDictionary<string, SshSession> _sessions = new();

    public SshSessionManager(ILogger<SshSessionManager> logger, IHubContext<SshTerminalHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task StartSessionAsync(string connectionId, string deviceId)
    {
        // Resolve Device Credentials (Mocked)
        var (host, user, pass) = GetMockCredentials(deviceId);

        _logger.LogInformation("Starting SSH session for connection {ConnectionId} to {Host}", connectionId, host);
        var session = new SshSession(connectionId, host, user, pass, _hubContext, _logger);   
        try
        {
            await session.ConnectAsync();
            if (!_sessions.TryAdd(connectionId, session))
            {
                await session.DisposeAsync();
                throw new InvalidOperationException("Session already exists for this connection.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SSH session for {ConnectionId}", connectionId);
            await session.DisposeAsync();
            throw;
        }
    }

    public async Task WriteInputAsync(string connectionId, string data)
    {
        if (_sessions.TryGetValue(connectionId, out var session))
        {
            await session.WriteAsync(data);
        }
        else
        {
            _logger.LogWarning("Session not found for connection {ConnectionId}", connectionId);
        }
    }

    public async Task ResizeTerminalAsync(string connectionId, int cols, int rows)
    {
        if (_sessions.TryGetValue(connectionId, out var session))
        {
            await session.ResizeAsync(cols, rows);
        }
    }

    public async Task EndSessionAsync(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var session))
        {
            _logger.LogInformation("Ending SSH session for {ConnectionId}", connectionId);
            await session.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        _sessions.Clear();
    }

    private (string host, string user, string pass) GetMockCredentials(string deviceId)
    {
        return ("192.168.1.100", "admin", "password"); 
    }

    private class SshSession : IAsyncDisposable
    {
        private readonly string _connectionId;
        private readonly string _host;
        private readonly string _username;
        private readonly string _password;
        private readonly IHubContext<SshTerminalHub> _hubContext;
        private readonly ILogger _logger;
        
        private SshClient? _client;
        private ShellStream? _shellStream;
        private CancellationTokenSource? _readCts;
        private Task? _readTask;

        public SshSession(string connectionId, string host, string username, string password, IHubContext<SshTerminalHub> hubContext, ILogger logger)
        {
            _connectionId = connectionId;
            _host = host;
            _username = username;
            _password = password;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task ConnectAsync()
        {
            _client = new SshClient(_host, _username, _password);
            await _client.ConnectAsync(CancellationToken.None);
            
            _shellStream = _client.CreateShellStream("xterm", 80, 24, 800, 600, 1024);
            _readCts = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
        }

        public async Task WriteAsync(string data)
        {
            if (_shellStream != null)
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                await _shellStream.WriteAsync(bytes, 0, bytes.Length);
                await _shellStream.FlushAsync();
            }
        }

        public Task ResizeAsync(int cols, int rows)
        {
            if (_shellStream != null)
            {
                try 
                {
                    var channelField = typeof(ShellStream).GetField("_channel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (channelField != null)
                    {
                        var channel = channelField.GetValue(_shellStream);
                        if (channel != null)
                        {
                            var sendWindowChangeMethod = channel.GetType().GetMethod("SendWindowChangeRequest");
                            if (sendWindowChangeMethod != null)
                            {
                                sendWindowChangeMethod.Invoke(channel, new object[] { (uint)cols, (uint)rows, (uint)0, (uint)0 });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resize terminal for {ConnectionId}", _connectionId);
                }
            }
            return Task.CompletedTask;
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            var buffer = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && _shellStream != null)
                {
                    int bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // End of stream

                    var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await _hubContext.Clients.Client(_connectionId).SendAsync("ReceiveOutput", text, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SSH stream for {ConnectionId}", _connectionId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _readCts?.Cancel();
            
            if (_readTask != null)
            {
                try { await _readTask; } catch { }
            }

            _readCts?.Dispose();
            _shellStream?.Dispose();
            _client?.Disconnect();
            _client?.Dispose();
        }
    }
}
