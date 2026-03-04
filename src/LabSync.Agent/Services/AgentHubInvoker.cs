using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace LabSync.Agent.Services;

public class AgentHubInvoker : IAgentHubInvoker
{
    private HubConnection? _hubConnection;
    private readonly List<Delegate> _remoteDesktopAnswerHandlers = new();
    private readonly List<Delegate> _remoteDesktopIceCandidateHandlers = new();
    private readonly List<Delegate> _startRemoteDesktopSessionHandlers = new();
    private readonly object _gate = new();

    public void AttachConnection(object hubConnection)
    {
        _hubConnection = (HubConnection)hubConnection;
        _hubConnection.On<Guid, string, string>("RemoteDesktopAnswer", (sessionId, sdpType, sdp) =>
        {
            lock (_gate)
            {
                foreach (var h in _remoteDesktopAnswerHandlers)
                    ((Action<Guid, string, string>)h)(sessionId, sdpType, sdp);
            }
        });
        _hubConnection.On<Guid, string, string?, int?>("RemoteDesktopIceCandidate", (sessionId, candidate, sdpMid, sdpMLineIndex) =>
        {
            lock (_gate)
            {
                foreach (var h in _remoteDesktopIceCandidateHandlers)
                    ((Action<Guid, string, string?, int?>)h)(sessionId, candidate, sdpMid, sdpMLineIndex);
            }
        });
        _hubConnection.On<Guid, RemoteDesktopPreferencesDto?>("StartRemoteDesktopSession", (sessionId, prefs) =>
        {
            lock (_gate)
            {
                foreach (var h in _startRemoteDesktopSessionHandlers)
                {
                    if (h is Action<Guid> action1) action1(sessionId);
                    else if (h is Action<Guid, RemoteDesktopPreferencesDto?> action2) action2(sessionId, prefs);
                }
            }
        });
    }

    public Task InvokeAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Hub is not connected.");
        return _hubConnection.InvokeCoreAsync(methodName, args, cancellationToken);
    }

    public void RegisterHandler<T1, T2, T3>(string methodName, Action<T1, T2, T3> handler)
    {
        if (methodName == "RemoteDesktopAnswer")
        {
            lock (_gate) _remoteDesktopAnswerHandlers.Add(handler);
        }
    }

    public void RegisterHandler<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> handler)
    {
        if (methodName == "RemoteDesktopIceCandidate")
        {
            lock (_gate) _remoteDesktopIceCandidateHandlers.Add(handler);
        }
    }

    public void RegisterHandler<T1, T2>(string methodName, Action<T1, T2> handler)
    {
        if (methodName == "StartRemoteDesktopSession")
        {
            lock (_gate) _startRemoteDesktopSessionHandlers.Add(handler);
        }
    }

    public void RegisterHandler<T1>(string methodName, Action<T1> handler)
    {
        if (methodName == "StartRemoteDesktopSession")
        {
            lock (_gate) _startRemoteDesktopSessionHandlers.Add(handler);
        }
    }
}
