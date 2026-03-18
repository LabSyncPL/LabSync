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
    private readonly List<Delegate> _stopRemoteDesktopSessionHandlers = new();
    private readonly List<Delegate> _startMonitorHandlers = new();
    private readonly List<Delegate> _stopMonitorHandlers = new();
    private readonly List<Delegate> _configureMonitorHandlers = new();

    // ── Input injection ───────────────────────────────────────────────────────
    private readonly List<Delegate> _mouseMoveHandlers = new();
    private readonly List<Delegate> _mouseButtonHandlers = new();
    private readonly List<Delegate> _mouseWheelHandlers = new();
    private readonly List<Delegate> _keyEventHandlers = new();
    private readonly List<Delegate> _charEventHandlers = new();

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
        _hubConnection.On<Guid>("StopRemoteDesktopSession", (sessionId) =>
        {
            lock (_gate)
            {
                foreach (var h in _stopRemoteDesktopSessionHandlers)
                    ((Action<Guid>)h)(sessionId);
            }
        });
        _hubConnection.On("StartMonitor", () =>
        {
            lock (_gate)
            {
                foreach (var h in _startMonitorHandlers) ((Action)h)();
            }
        });
        _hubConnection.On("StopMonitor", () =>
        {
            lock (_gate)
            {
                foreach (var h in _stopMonitorHandlers) ((Action)h)();
            }
        });
        _hubConnection.On<int, int, int>("ConfigureMonitor", (width, quality, fps) =>
        {
            lock (_gate)
            {
                foreach (var h in _configureMonitorHandlers)
                    ((Action<int, int, int>)h)(width, quality, fps);
            }
        });

        // ── Input injection ───────────────────────────────────────────────────
        _hubConnection.On<double, double>("MouseMove", (nx, ny) =>
        {
            lock (_gate)
                foreach (var h in _mouseMoveHandlers)
                    ((Action<double, double>)h)(nx, ny);
        });
        _hubConnection.On<int, bool>("MouseButton", (btn, down) =>
        {
            lock (_gate)
                foreach (var h in _mouseButtonHandlers)
                    ((Action<int, bool>)h)(btn, down);
        });
        _hubConnection.On<int>("MouseWheel", delta =>
        {
            lock (_gate)
                foreach (var h in _mouseWheelHandlers)
                    ((Action<int>)h)(delta);
        });
        _hubConnection.On<ushort, bool>("KeyEvent", (vk, down) =>
        {
            lock (_gate)
                foreach (var h in _keyEventHandlers)
                    ((Action<ushort, bool>)h)(vk, down);
        });
        _hubConnection.On<char, bool>("CharEvent", (ch, down) =>
        {
            lock (_gate)
                foreach (var h in _charEventHandlers)
                    ((Action<char, bool>)h)(ch, down);
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
            lock (_gate) { _remoteDesktopAnswerHandlers.Add(handler); }
        else if (methodName == "ConfigureMonitor")
            lock (_gate) { _configureMonitorHandlers.Add(handler); }
    }

    public void RegisterHandler<T1, T2, T3, T4>(string methodName, Action<T1, T2, T3, T4> handler)
    {
        if (methodName == "RemoteDesktopIceCandidate")
            lock (_gate) { _remoteDesktopIceCandidateHandlers.Add(handler); }
    }

    public void RegisterHandler<T1, T2>(string methodName, Action<T1, T2> handler)
    {
        if (methodName == "StartRemoteDesktopSession")
            lock (_gate) { _startRemoteDesktopSessionHandlers.Add(handler); }
        else if (methodName == "MouseMove")
            lock (_gate) { _mouseMoveHandlers.Add(handler); }
        else if (methodName == "MouseButton")
            lock (_gate) { _mouseButtonHandlers.Add(handler); }
        else if (methodName == "KeyEvent")
            lock (_gate) { _keyEventHandlers.Add(handler); }
        else if (methodName == "CharEvent")
            lock (_gate) { _charEventHandlers.Add(handler); }
    }

    public void RegisterHandler<T1>(string methodName, Action<T1> handler)
    {
        if (methodName == "StartRemoteDesktopSession")
            lock (_gate) { _startRemoteDesktopSessionHandlers.Add(handler); }
        else if (methodName == "StopRemoteDesktopSession")
            lock (_gate) { _stopRemoteDesktopSessionHandlers.Add(handler); }
        else if (methodName == "MouseWheel")
            lock (_gate) { _mouseWheelHandlers.Add(handler); }
    }

    public void RegisterHandler(string methodName, Action handler)
    {
        if (methodName == "StartMonitor")
            lock (_gate) { _startMonitorHandlers.Add(handler); }
        else if (methodName == "StopMonitor")
            lock (_gate) { _stopMonitorHandlers.Add(handler); }
    }
}