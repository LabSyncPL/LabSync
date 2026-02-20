using System.Net.Http.Json;
using LabSync.Core.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Services
{
    public class ServerClient : IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServerClient> _logger;
        private HubConnection? _hubConnection;

        public Action<Guid, string, string>? OnReceiveJob;

        public ServerClient(HttpClient httpClient, ILogger<ServerClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _logger.LogInformation("ServerClient initialized for {ServerUrl}", _httpClient.BaseAddress);
        }

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
                        _logger.LogWarning("Registration successful, but server returned no token. Device may be pending approval. Message: '{Message}'", result?.Message);
                        return null;
                    }
                    
                    _logger.LogInformation("Registration successful! Token received.");
                    return result.Token;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Registration failed. Status: {StatusCode}. Error: {Error}",
                        response.StatusCode, error);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Could not connect to the server at {BaseAddress}. Please check the URL and network connection.", _httpClient.BaseAddress);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during agent registration.");
                return null;
            }
        }

        public async Task ConnectAsync(string token, CancellationToken cancellationToken)
        {
            if (_hubConnection is not null && _hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogWarning("Hub connection is already established or connecting.");
                return;
            }

            var hubUrl = new Uri(_httpClient.BaseAddress!, "agenthub");
            _logger.LogInformation("Connecting to SignalR Hub at {HubUrl}", hubUrl);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<Guid, string, string>("ReceiveJob", (jobId, command, arguments) =>
            {
                _logger.LogInformation("Received job from server. JobId: {JobId}, Command: {Command}", jobId, command);
                OnReceiveJob?.Invoke(jobId, command, arguments);
            });

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

            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("SignalR Hub connection established successfully.");
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

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
            }
        }

        /// <summary>
        /// Odbiera wyniki wykonanego zadania od Agenta.
        /// </summary>
        public async Task UploadJobResult(JobResultDto result)
        {
            _logger.LogInformation("Agent reported result for Job {JobId}. Exit code: {ExitCode}" 
                , result.JobId, result.ExitCode);
        }
    }
}