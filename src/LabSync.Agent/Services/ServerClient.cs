using System.Net.Http.Json;
using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LabSync.Agent.Services;

public class ServerClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerClient> _logger;
    private readonly AgentContext _agentContext;
    private readonly IAgentHubInvoker? _hubInvoker;
    private HubConnection? _hubConnection;

    public ServerClient(HttpClient httpClient, ILogger<ServerClient> logger, AgentContext agentContext, IAgentHubInvoker? hubInvoker = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _agentContext = agentContext;
        _hubInvoker = hubInvoker;
    }
    public Action<Guid, string, string, string?>? OnReceiveJob;
    public Action<Guid>? OnStartRemoteDesktopSession;

    public async Task<string?> RegisterAgentAsync(RegisterAgentRequest request)
    {
        try
        {
            _logger.LogInformation("Sending registration request to {BaseAddress}...", _httpClient.BaseAddress);
            var response = await _httpClient.PostAsJsonAsync("api/agents/register", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterAgentResponse>();
                if (string.IsNullOrEmpty(result?.Token))
                {
                    _logger.LogWarning("Registration successful, but server returned no token. Message: '{Message}'", result?.Message);
                    return null;
                }

                _agentContext.SetDeviceId(result.DeviceId);
                _logger.LogInformation("Registration successful! Token received. DeviceId: {DeviceId}", result.DeviceId);
                return result.Token;
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Registration failed. Status: {StatusCode}. Error: {Error}", response.StatusCode, error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error.");
            return null;
        }
    }

    public async Task ConnectAsync(string token, CancellationToken cancellationToken)
    {
        if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected)
            return;

        var hubUrl = new Uri(_httpClient.BaseAddress!, "agenthub");
        _logger.LogInformation("Connecting to SignalR Hub at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid, string, string, string?>("ReceiveJob", (jobId, command, arguments, scriptPayload) =>
        {
            _logger.LogInformation("Received job from server. JobId: {JobId}, Command: {Command}", jobId, command);
            OnReceiveJob?.Invoke(jobId, command, arguments, scriptPayload);
        });

        _hubConnection.On("Ping", () => _logger.LogDebug("Received Ping from server."));

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Hub connection lost. Attempting to reconnect...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Hub connection re-established. Connection ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubInvoker?.AttachConnection(_hubConnection);

        await _hubConnection.StartAsync(cancellationToken);
        _logger.LogInformation("SignalR Hub connection established successfully.");
    }

    public async Task SendRemoteDesktopOfferAsync(Guid sessionId, string sdpType, string sdp)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
            return;
        await _hubConnection.InvokeAsync("RemoteDesktopOffer", sessionId, Guid.Empty, sdpType, sdp);
    }

    public async Task SendRemoteDesktopIceCandidateAsync(Guid sessionId, string candidate)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
            return;
        await _hubConnection.InvokeAsync("RemoteDesktopIceCandidate", sessionId, candidate, null, 0);
    }

    public async Task ReportJobResultAsync(JobResultDto result)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            _logger.LogError("Cannot report job result. Hub connection is not active.");
            return;
        }
        await _hubConnection.InvokeAsync("UploadJobResult", result);
    }

    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
            return;

        try { await _hubConnection.InvokeAsync("Heartbeat", cancellationToken); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogDebug(ex, "Heartbeat failed."); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

}