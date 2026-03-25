using System;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace LabSync.Modules.SSH.Interfaces;

/// <summary>
/// Provides an interactive remote shell session over SSH.
/// Wraps SSH.NET's ShellStream functionality.
/// </summary>
public interface IRemoteShellService : IAsyncDisposable
{
    /// <summary>
    /// Event triggered when new data is received from the remote shell output.
    /// </summary>
    event EventHandler<ShellOutputEventArgs> OutputReceived;

    /// <summary>
    /// Opens an interactive shell session to the remote host using SSH key authentication.
    /// </summary>
    /// <param name="sessionId">Client-provided session identifier.</param>
    /// <param name="host">The remote host address.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="keyFile">The private key file for authentication.</param>
    /// <param name="terminalName">The terminal emulation type (default: "xterm").</param>
    /// <param name="columns">The number of columns in the terminal window.</param>
    /// <param name="rows">The number of rows in the terminal window.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the session initialization.</returns>
    Task OpenSessionAsync(string sessionId, string host, string username, PrivateKeyFile keyFile, string terminalName = "xterm", uint columns = 80, uint rows = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a command or input string to a shell session input stream.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="input">The string to write to the shell.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the write operation.</returns>
    Task WriteAsync(string sessionId, string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes an active terminal session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="columns">New column count.</param>
    /// <param name="rows">New row count.</param>
    Task ResizeTerminalAsync(string sessionId, int columns, int rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes an active terminal session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
