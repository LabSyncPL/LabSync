using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Modules.SSH.Interfaces;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LabSync.Modules.SSH.Services;

public class TunnelingService : ITunnelingService, IDisposable
{
    private readonly ILogger<TunnelingService> _logger;
    private readonly ConcurrentDictionary<string, SshClient> _activeTunnels = new();

    public TunnelingService(ILogger<TunnelingService> logger)
    {
        _logger = logger;
    }

    public async Task StartLocalForwardingAsync(string host, string username, string password, string boundHost, uint boundPort, string remoteHost, uint remotePort, CancellationToken cancellationToken = default)
    {
        string tunnelKey = GetTunnelKey(boundHost, boundPort);

        if (_activeTunnels.ContainsKey(tunnelKey))
        {
            _logger.LogWarning("Tunnel {TunnelKey} already exists.", tunnelKey);
            return;
        }

        _logger.LogInformation("Starting tunnel {TunnelKey} -> {RemoteHost}:{RemotePort} via {Host}", tunnelKey, remoteHost, remotePort, host);

        var client = new SshClient(host, username, password);
        
        try
        {
            await client.ConnectAsync(cancellationToken);

            var portForwarded = new ForwardedPortLocal(boundHost, boundPort, remoteHost, remotePort);
            client.AddForwardedPort(portForwarded);
            
            portForwarded.Exception += (sender, e) => 
            {
                _logger.LogError(e.Exception, "Error on forwarded port {TunnelKey}", tunnelKey);
            };

            portForwarded.Start();

            if (!_activeTunnels.TryAdd(tunnelKey, client))
            {
                portForwarded.Stop();
                client.Disconnect();
                client.Dispose();
                throw new InvalidOperationException($"Tunnel {tunnelKey} already exists.");
            }

            _logger.LogInformation("Tunnel {TunnelKey} started successfully.", tunnelKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start tunnel {TunnelKey}", tunnelKey);
            client.Dispose();
            throw;
        }
    }

    public Task StopLocalForwardingAsync(string boundHost, uint boundPort, CancellationToken cancellationToken = default)
    {
        string tunnelKey = GetTunnelKey(boundHost, boundPort);
        _logger.LogInformation("Stopping tunnel {TunnelKey}", tunnelKey);

        if (_activeTunnels.TryRemove(tunnelKey, out var client))
        {
            try
            {
                foreach (var port in client.ForwardedPorts)
                {
                    if (port.IsStarted)
                    {
                        port.Stop();
                    }
                }
                client.Disconnect();
                client.Dispose();
                _logger.LogInformation("Tunnel {TunnelKey} stopped.", tunnelKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping tunnel {TunnelKey}", tunnelKey);
            }
        }
        else
        {
            _logger.LogWarning("Tunnel {TunnelKey} not found.", tunnelKey);
        }

        return Task.CompletedTask;
    }

    private string GetTunnelKey(string boundHost, uint boundPort) => $"{boundHost}:{boundPort}";

    public void Dispose()
    {
        foreach (var key in _activeTunnels.Keys)
        {
            StopLocalForwardingAsync(key.Split(':')[0], uint.Parse(key.Split(':')[1])).GetAwaiter().GetResult();
        }
    }
}
