using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Core.Interfaces;
using LabSync.Modules.SSH.Interfaces;
using LabSync.Modules.SSH.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SSH;

public class SshModule : IAgentModule, IAsyncDisposable
{
    public string Name => "SSH";
    public string Version => "1.0.0";

    private ILogger<SshModule>? _logger;
    private IKeyManagementService? _keyManagementService;
    private IFileTransferService? _fileTransferService;
    private ITunnelingService? _tunnelingService;
    private IRemoteShellService? _remoteShellService;
    
    private string? _defaultKeyPath;
    private readonly List<IAsyncDisposable> _disposables = new();

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger<SshModule>();

        _logger.LogInformation("Initializing SSH Module...");

        _keyManagementService = serviceProvider.GetService<IKeyManagementService>() 
            ?? new KeyManagementService(loggerFactory.CreateLogger<KeyManagementService>());

        _defaultKeyPath = await _keyManagementService.EnsureKeyAsync();

        _fileTransferService = serviceProvider.GetService<IFileTransferService>() 
            ?? new FileTransferService(loggerFactory.CreateLogger<FileTransferService>(), _keyManagementService);

        _tunnelingService = serviceProvider.GetService<ITunnelingService>();
        if (_tunnelingService == null)
        {
            var ts = new TunnelingService(loggerFactory.CreateLogger<TunnelingService>(), _keyManagementService);
            _tunnelingService = ts;
            _disposables.Add(ts as IAsyncDisposable ?? new AsyncDisposableWrapper(ts));
        }

        _remoteShellService = serviceProvider.GetService<IRemoteShellService>();
        if (_remoteShellService == null)
        {
            var rs = new RemoteShellService(loggerFactory.CreateLogger<RemoteShellService>(), _keyManagementService);
            _remoteShellService = rs;
            _disposables.Add(rs);
        }

        _logger.LogInformation("SSH Module initialized successfully.");
    }

    public bool CanHandle(string jobType)
    {
        return jobType.StartsWith("SSH:", StringComparison.OrdinalIgnoreCase) ||
               jobType.Equals("SshCommand", StringComparison.OrdinalIgnoreCase) ||
               jobType.Equals("SftpTransfer", StringComparison.OrdinalIgnoreCase) ||
               jobType.Equals("SshTunnel", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        try
        {
            // Convention used across modules:
            // - SystemInfoModule reads "__Command"
            // - SSH module historically reads "Command"
            // Prefer "__Command" when present to be consistent.
            string action = "Unknown";
            if (parameters.TryGetValue("__Command", out var commandFromInternal))
            {
                action = commandFromInternal;
            }
            else if (parameters.TryGetValue("Command", out var commandFromJob))
            {
                action = commandFromJob;
            }

            _logger?.LogInformation("Executing SSH action: {Action}", action);

            switch (action.ToLowerInvariant())
            {
                case "upload":
                    await ExecuteUploadAsync(parameters, cancellationToken);
                    return ModuleResult.Success("File uploaded successfully.");
                
                case "download":
                    await ExecuteDownloadAsync(parameters, cancellationToken);
                    return ModuleResult.Success("File downloaded successfully.");

                case "start-tunnel":
                    await ExecuteStartTunnelAsync(parameters, cancellationToken);
                    return ModuleResult.Success("Tunnel started.");

                case "stop-tunnel":
                    await ExecuteStopTunnelAsync(parameters, cancellationToken);
                    return ModuleResult.Success("Tunnel stopped.");

                case "get-public-key":
                    var keyPath = parameters.TryGetValue("KeyPath", out var kp) ? kp : _defaultKeyPath;
                    if (string.IsNullOrEmpty(keyPath)) return ModuleResult.Failure("Key path not found.");
                    var pubKey = _keyManagementService!.GetPublicKey(keyPath);
                    return ModuleResult.Success(pubKey);

                // ── Remote terminal (interactive shell) ───────────────────────
                case "terminal-open":
                case "open-terminal":
                    await ExecuteTerminalOpenAsync(parameters, cancellationToken);
                    return ModuleResult.Success(new { Opened = true, SessionId = GetSessionId(parameters) });

                case "terminal-write":
                case "write-terminal":
                    await ExecuteTerminalWriteAsync(parameters, cancellationToken);
                    return ModuleResult.Success("Input written to terminal.");

                case "terminal-resize":
                case "resize-terminal":
                    await ExecuteTerminalResizeAsync(parameters, cancellationToken);
                    return ModuleResult.Success("Terminal resized.");

                case "terminal-close":
                case "close-terminal":
                    await ExecuteTerminalCloseAsync(parameters, cancellationToken);
                    return ModuleResult.Success("Terminal session closed.");
          
                default:
                    return ModuleResult.Failure($"Unknown SSH action: {action}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing SSH module.");
            return ModuleResult.Failure(ex.Message);
        }
    }

    private Renci.SshNet.PrivateKeyFile GetKeyFile(IDictionary<string, string> parameters)
    {
        string? keyPath = parameters.TryGetValue("KeyPath", out var kp) ? kp : _defaultKeyPath;
        string? passphrase = parameters.TryGetValue("Passphrase", out var pp) ? pp : null;
        
        if (string.IsNullOrEmpty(keyPath)) throw new InvalidOperationException("No private key available.");
        
        return _keyManagementService!.GetPrivateKeyFile(keyPath, passphrase);
    }

    private static string GetSessionId(IDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("SessionId", out var sid) && !string.IsNullOrWhiteSpace(sid)) return sid;
        if (parameters.TryGetValue("ConnectionId", out var cid) && !string.IsNullOrWhiteSpace(cid)) return cid;
        if (parameters.TryGetValue("connectionId", out var cid2) && !string.IsNullOrWhiteSpace(cid2)) return cid2;
        return "default";
    }

    private static string GetRequired(IDictionary<string, string> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required SSH parameter: {key}");
        return value;
    }

    private async Task ExecuteUploadAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        var keyFile = GetKeyFile(parameters);
        await _fileTransferService!.UploadFileAsync(
            GetRequired(parameters, "Host"),
            GetRequired(parameters, "Username"),
            keyFile, 
            GetRequired(parameters, "LocalPath"),
            GetRequired(parameters, "RemotePath"),
            token);
    }

    private async Task ExecuteDownloadAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        var keyFile = GetKeyFile(parameters);
        await _fileTransferService!.DownloadFileAsync(
            GetRequired(parameters, "Host"),
            GetRequired(parameters, "Username"),
            keyFile, 
            GetRequired(parameters, "RemotePath"),
            GetRequired(parameters, "LocalPath"),
            token);
    }

    private async Task ExecuteStartTunnelAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        var keyFile = GetKeyFile(parameters);
        await _tunnelingService!.StartLocalForwardingAsync(
            GetRequired(parameters, "Host"),
            GetRequired(parameters, "Username"),
            keyFile,
            GetRequired(parameters, "BoundHost"),
            uint.Parse(GetRequired(parameters, "BoundPort")),
            GetRequired(parameters, "RemoteHost"),
            uint.Parse(GetRequired(parameters, "RemotePort")),
            token);
    }

    private async Task ExecuteStopTunnelAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        await _tunnelingService!.StopLocalForwardingAsync(
            GetRequired(parameters, "BoundHost"),
            uint.Parse(GetRequired(parameters, "BoundPort")),
            token);
    }

    private async Task ExecuteTerminalOpenAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        if (_remoteShellService == null)
            throw new InvalidOperationException("RemoteShellService not initialized.");

        var sessionId = GetSessionId(parameters);
        var host = GetRequired(parameters, "Host");
        var username = GetRequired(parameters, "Username");
        var keyFile = GetKeyFile(parameters);

        var terminalName = parameters.TryGetValue("TerminalName", out var tn) && !string.IsNullOrWhiteSpace(tn)
            ? tn
            : "xterm";

        uint columns = 80;
        uint rows = 24;
        if (parameters.TryGetValue("Columns", out var c) && uint.TryParse(c, out var parsedC)) columns = parsedC;
        if (parameters.TryGetValue("Rows", out var r) && uint.TryParse(r, out var parsedR)) rows = parsedR;

        await _remoteShellService.OpenSessionAsync(sessionId, host, username, keyFile, terminalName, columns, rows, token);
    }

    private async Task ExecuteTerminalWriteAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        if (_remoteShellService == null)
            throw new InvalidOperationException("RemoteShellService not initialized.");

        var sessionId = GetSessionId(parameters);
        if (!parameters.TryGetValue("Input", out var input) || string.IsNullOrEmpty(input))
        {
            // Backward-friendly alias
            parameters.TryGetValue("Data", out input);
        }

        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Missing required SSH parameter: Input");

        await _remoteShellService.WriteAsync(sessionId, input, token);
    }

    private Task ExecuteTerminalResizeAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        if (_remoteShellService == null)
            throw new InvalidOperationException("RemoteShellService not initialized.");

        var sessionId = GetSessionId(parameters);
        var columns = int.Parse(GetRequired(parameters, "Columns"));
        var rows = int.Parse(GetRequired(parameters, "Rows"));
        return _remoteShellService.ResizeTerminalAsync(sessionId, columns, rows, token);
    }

    private Task ExecuteTerminalCloseAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        if (_remoteShellService == null)
            throw new InvalidOperationException("RemoteShellService not initialized.");

        var sessionId = GetSessionId(parameters);
        return _remoteShellService.CloseSessionAsync(sessionId, token);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
        _disposables.Clear();
    }

    private class AsyncDisposableWrapper : IAsyncDisposable
    {
        private readonly IDisposable _disposable;
        public AsyncDisposableWrapper(IDisposable disposable) => _disposable = disposable;
        public ValueTask DisposeAsync()
        {
            _disposable.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
