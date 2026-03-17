using Microsoft.AspNetCore.SignalR;
using LabSync.Server.Services;

namespace LabSync.Server.Hubs;

public class SshTerminalHub : Hub
{
    private readonly SshSessionManager _sessionManager;
    private readonly ILogger<SshTerminalHub> _logger;

    public SshTerminalHub(SshSessionManager sessionManager, ILogger<SshTerminalHub> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task ConnectToDevice(string deviceId)
    {
        try
        {
            await _sessionManager.StartSessionAsync(Context.ConnectionId, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device {DeviceId}", deviceId);
            await Clients.Caller.SendAsync("ErrorMessage", "Failed to connect to SSH session.");
        }
    }

    public async Task SendInput(string data)
    {
        await _sessionManager.WriteInputAsync(Context.ConnectionId, data);
    }

    public async Task ResizeTerminal(int columns, int rows)
    {
        await _sessionManager.ResizeTerminalAsync(Context.ConnectionId, columns, rows);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _sessionManager.EndSessionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
