using System.Threading;
using System.Threading.Tasks;

namespace LabSync.Modules.SSH.Interfaces;

/// <summary>
/// Provides SSH tunneling and port forwarding capabilities.
/// Wraps SSH.NET's forwarding functionality.
/// </summary>
public interface ITunnelingService
{
    /// <summary>
    /// Starts a local port forwarding tunnel.
    /// Traffic sent to the local port is forwarded through the SSH tunnel to the remote destination.
    /// </summary>
    /// <param name="host">The SSH server host.</param>
    /// <param name="username">The SSH username.</param>
    /// <param name="password">The SSH password.</param>
    /// <param name="boundHost">The local interface to bind to (e.g., "127.0.0.1").</param>
    /// <param name="boundPort">The local port to listen on.</param>
    /// <param name="remoteHost">The destination host reachable from the SSH server.</param>
    /// <param name="remotePort">The destination port on the remote host.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the tunnel initialization.</returns>
    Task StartLocalForwardingAsync(string host, string username, string password, string boundHost, uint boundPort, string remoteHost, uint remotePort, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active local port forwarding tunnel.
    /// </summary>
    /// <param name="boundHost">The local interface the tunnel is bound to.</param>
    /// <param name="boundPort">The local port the tunnel is listening on.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the stop operation.</returns>
    Task StopLocalForwardingAsync(string boundHost, uint boundPort, CancellationToken cancellationToken = default);
}
