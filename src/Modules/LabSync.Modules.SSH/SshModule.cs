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
    private IFileTransferService? _fileTransferService;
    private ITunnelingService? _tunnelingService;
    private IRemoteShellService? _remoteShellService;
    
    private readonly List<IAsyncDisposable> _disposables = new();

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger<SshModule>();

        _logger.LogInformation("Initializing SSH Module...");

        _fileTransferService = serviceProvider.GetService<IFileTransferService>();
        _tunnelingService = serviceProvider.GetService<ITunnelingService>();
        _remoteShellService = serviceProvider.GetService<IRemoteShellService>();
        if (_fileTransferService == null)
        {
            _fileTransferService = new FileTransferService(loggerFactory.CreateLogger<FileTransferService>());
        }

        if (_tunnelingService == null)
        {
            var ts = new TunnelingService(loggerFactory.CreateLogger<TunnelingService>());
            _tunnelingService = ts;
            _disposables.Add(ts as IAsyncDisposable ?? new AsyncDisposableWrapper(ts));
        }

        if (_remoteShellService == null)
        {
            var rs = new RemoteShellService(loggerFactory.CreateLogger<RemoteShellService>());
            _remoteShellService = rs;
            _disposables.Add(rs);
        }

        _logger.LogInformation("SSH Module initialized successfully.");
        await Task.CompletedTask;
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
            if (!parameters.TryGetValue("Command", out var command))
            {
                command = "Unknown";
            }

            _logger?.LogInformation("Executing SSH command: {Command}", command);

            switch (command.ToLowerInvariant())
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
          
                default:
                    return ModuleResult.Failure($"Unknown SSH command: {command}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing SSH module.");
            return ModuleResult.Failure(ex.Message);
        }
    }

    private async Task ExecuteUploadAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        await _fileTransferService!.UploadFileAsync(
            parameters["Host"], 
            parameters["Username"], 
            parameters["Password"], 
            parameters["LocalPath"], 
            parameters["RemotePath"], 
            token);
    }

    private async Task ExecuteDownloadAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        await _fileTransferService!.DownloadFileAsync(
            parameters["Host"], 
            parameters["Username"], 
            parameters["Password"], 
            parameters["RemotePath"], 
            parameters["LocalPath"], 
            token);
    }

    private async Task ExecuteStartTunnelAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        await _tunnelingService!.StartLocalForwardingAsync(
            parameters["Host"],
            parameters["Username"],
            parameters["Password"],
            parameters["BoundHost"],
            uint.Parse(parameters["BoundPort"]),
            parameters["RemoteHost"],
            uint.Parse(parameters["RemotePort"]),
            token);
    }

    private async Task ExecuteStopTunnelAsync(IDictionary<string, string> parameters, CancellationToken token)
    {
        await _tunnelingService!.StopLocalForwardingAsync(
            parameters["BoundHost"],
            uint.Parse(parameters["BoundPort"]),
            token);
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
