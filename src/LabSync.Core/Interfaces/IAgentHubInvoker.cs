namespace LabSync.Core.Interfaces;

/// <summary>
/// Allows agent modules to invoke SignalR hub methods and subscribe to hub events
/// without depending on the agent host. The host registers an implementation
/// that delegates to the existing SignalR connection.
/// </summary>
public interface IAgentHubInvoker
{
    void AttachConnection(object hubConnection);
    Task InvokeAsync(string methodName, object?[] args, CancellationToken cancellationToken = default);
    void RegisterHandler<T1, T2, T3>(string methodName, Action<T1, T2, T3> handler);
    void RegisterHandler<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> handler);
}
