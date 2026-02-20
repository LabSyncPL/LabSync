using System.Net.Http.Json;
using LabSync.Core.Dto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LabSync.Agent.Services
{
    public class ServerClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServerClient> _logger;

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
    }
}