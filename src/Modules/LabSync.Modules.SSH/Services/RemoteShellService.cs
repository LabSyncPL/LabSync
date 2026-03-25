using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
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
    private readonly IKeyManagementService _keyService;

    private readonly ConcurrentDictionary<string, ShellSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ShellOutputEventArgs>? OutputReceived;

    public RemoteShellService(ILogger<RemoteShellService> logger, IKeyManagementService keyService)
    {
        _logger = logger;
        _keyService = keyService;
    }

    public async Task OpenSessionAsync(
        string sessionId,
        string host,
        string username,
        PrivateKeyFile keyFile,
        string terminalName = "xterm",
        uint columns = 80,
        uint rows = 24,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("SessionId is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));

        if (_sessions.ContainsKey(sessionId))
        {
            throw new InvalidOperationException($"Terminal session '{sessionId}' already open.");
        }

        _logger.LogInformation(
            "Opening SSH terminal session {SessionId} to {Host} with terminal {Terminal}",
            sessionId, host, terminalName);

        var client = new SshClient(host, username, keyFile);
        client.HostKeyReceived += (sender, e) =>
        {
            try
            {
                _keyService.ValidateOrAddHostKey(host, e.HostKeyName, e.HostKey);
                e.CanTrust = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host key validation failed for {Host}", host);
                e.CanTrust = false;
            }
        };

        var shellStream = default(ShellStream);
        CancellationTokenSource? readCts = null;
        Task? readTask = null;
        try
        {
            await client.ConnectAsync(cancellationToken);
            shellStream = client.CreateShellStream(terminalName, columns, rows, 0, 0, 1024);

            // Linked token ensures OpenSessionAsync can be cancelled while establishing the session.
            readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTask = Task.Run(() => ReadLoopAsync(sessionId, shellStream, readCts.Token), CancellationToken.None);
        }
        catch
        {
            client.Dispose();
            shellStream?.Dispose();
            readCts?.Dispose();
            throw;
        }
        
        if (shellStream is null || readCts is null || readTask is null)
            throw new InvalidOperationException("Failed to initialize SSH shell session state.");

        var session = new ShellSession(sessionId, client, shellStream, readCts, readTask);
        if (!_sessions.TryAdd(sessionId, session))
        {
            // Extremely defensive: race protection.
            await session.DisposeAsync();
            throw new InvalidOperationException($"Terminal session '{sessionId}' already open.");
        }
    }

    private async Task ReadLoopAsync(string sessionId, ShellStream shellStream, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        var decoder = Encoding.UTF8.GetDecoder();
        var charBuffer = ArrayPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(buffer.Length));

        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await shellStream.ReadAsync(buffer, 0, buffer.Length, token);
                
                if (bytesRead == 0)
                {
                    break;
                }

                int charsDecoded = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                if (charsDecoded > 0)
                {
                    var text = new string(charBuffer, 0, charsDecoded);
                    OutputReceived?.Invoke(this, new ShellOutputEventArgs(sessionId, text));
                }
            }
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from SSH shell stream for session {SessionId}", sessionId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<char>.Shared.Return(charBuffer);
        }
    }

    public async Task WriteAsync(string sessionId, string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input)) return;
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Terminal session '{sessionId}' not open.");
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(input);
            await session.WriteGate.WaitAsync(cancellationToken);
            try
            {
                await session.ShellStream.WriteAsync(data, 0, data.Length, cancellationToken);
                await session.ShellStream.FlushAsync(cancellationToken);
            }
            finally
            {
                session.WriteGate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to SSH shell for session {SessionId}", sessionId);
            throw;
        }
    }

    public Task ResizeTerminalAsync(string sessionId, int columns, int rows, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Terminal session '{sessionId}' not open.");
        }

        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

        // Note: resizing relies on SSH.NET internals (reflection), similar to server-side implementation.
        try
        {
            var channelField = typeof(ShellStream).GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance);
            if (channelField != null)
            {
                var channel = channelField.GetValue(session.ShellStream);
                if (channel != null)
                {
                    var sendWindowChangeMethod = channel.GetType().GetMethod("SendWindowChangeRequest");
                    sendWindowChangeMethod?.Invoke(channel, new object[] { (uint)columns, (uint)rows, (uint)0, (uint)0 });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resize terminal for session {SessionId}", sessionId);
        }

        return Task.CompletedTask;
    }

    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session)) return;
        await session.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var kv in _sessions)
        {
            await kv.Value.DisposeAsync();
        }
        _sessions.Clear();
    }

    private sealed class ShellSession
    {
        public string SessionId { get; }
        public SshClient Client { get; }
        public ShellStream ShellStream { get; }
        public CancellationTokenSource ReadCts { get; }
        public Task ReadTask { get; }

        // Lightweight gate to avoid concurrent writes during resize/dispose.
        public SemaphoreSlim WriteGate { get; } = new(1, 1);

        public ShellSession(string sessionId, SshClient client, ShellStream shellStream, CancellationTokenSource readCts, Task readTask)
        {
            SessionId = sessionId;
            Client = client;
            ShellStream = shellStream;
            ReadCts = readCts;
            ReadTask = readTask;
        }

        public async ValueTask DisposeAsync()
        {
            try { ReadCts.Cancel(); } catch { }

            try
            {
                await ReadTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore read loop exceptions during shutdown.
            }

            try { ReadCts.Dispose(); } catch { }
            try { ShellStream.Dispose(); } catch { }
            try { Client.Disconnect(); } catch { }
            try { Client.Dispose(); } catch { }
            try { WriteGate.Dispose(); } catch { }
        }
    }
}
