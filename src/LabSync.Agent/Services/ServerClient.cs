using System.Net.Http.Json;
using LabSync.Core.Dto;

namespace LabSync.Agent.Services
{
    public class ServerClient
    {
        private readonly HttpClient            _httpClient;
        private readonly ILogger<ServerClient> _logger;

        public ServerClient(HttpClient httpClient, ILogger<ServerClient> logger)
        {
            _httpClient = httpClient;
            _logger     = logger;
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
                    _logger.LogInformation("Registration successful! DeviceID: {DeviceId}", result?.DeviceId);
                    return result?.Token;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Registration failed. Status: {StatusCode}. Error: {Error}", response.StatusCode, error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not connect to the server.");
                return null;
            }
        }
    }
}