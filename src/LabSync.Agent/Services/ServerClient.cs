using System.Net.Http.Json;
using LabSync.Core.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LabSync.Agent.Services;

public class ServerClient(HttpClient httpClient, ILogger<ServerClient> logger) : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    public Action<Guid, string, string, string?>? OnReceiveJob;

    public async Task<string?> RegisterAgentAsync(RegisterAgentRequest request)
    {
        try
        {
            logger.LogInformation("Sending registration request to {BaseAddress}...", httpClient.BaseAddress);
            var response = await httpClient.PostAsJsonAsync("api/agents/register", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RegisterAgentResponse>();
                if (string.IsNullOrEmpty(result?.Token))
                {
                    logger.LogWarning("Registration successful, but server returned no token. Message: '{Message}'", result?.Message);
                    return null;
                }

                logger.LogInformation("Registration successful! Token received.");
                return result.Token;
            }

            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Registration failed. Status: {StatusCode}. Error: {Error}", response.StatusCode, error);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Registration error.");
            return null;
        }
    }

    public async Task ConnectAsync(string token, CancellationToken cancellationToken)
    {
        if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected)
            return;

        var hubUrl = new Uri(httpClient.BaseAddress!, "agenthub");
        logger.LogInformation("Connecting to SignalR Hub at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<Guid, string, string, string?>("ReceiveJob", (jobId, command, arguments, scriptPayload) =>
        {
            logger.LogInformation("Received job from server. JobId: {JobId}, Command: {Command}", jobId, command);
            OnReceiveJob?.Invoke(jobId, command, arguments, scriptPayload);
        });

        _hubConnection.On("Ping", () => logger.LogDebug("Received Ping from server."));

        _hubConnection.Reconnecting += error =>
        {
            logger.LogWarning(error, "Hub connection lost. Attempting to reconnect...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            logger.LogInformation("Hub connection re-established. Connection ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        await _hubConnection.StartAsync(cancellationToken);
        logger.LogInformation("SignalR Hub connection established successfully.");
    }

    public async Task ReportJobResultAsync(JobResultDto result)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            logger.LogError("Cannot report job result. Hub connection is not active.");
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
        catch (Exception ex) { logger.LogDebug(ex, "Heartbeat failed."); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null) await _hubConnection.DisposeAsync();
    }

}