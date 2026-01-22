using System.Net.Http.Json;
using LabSync.Core.Dto;
using Microsoft.Extensions.Configuration;  // Dodaj using jeśli nie ma

namespace LabSync.Agent.Services
{
    public class ServerClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServerClient> _logger;
        private readonly string _serverUrl;  // Dodaj pole

        public ServerClient(HttpClient httpClient, ILogger<ServerClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Możesz też pobrać URL z konfiguracji dla logowania
            _serverUrl = Environment.GetEnvironmentVariable("AGENT_SERVER_URL") 
                ?? configuration["ServerUrl"] 
                ?? httpClient.BaseAddress?.ToString() 
                ?? "Nieznany URL";
            
            _logger.LogInformation("ServerClient zainicjalizowany dla {ServerUrl}", _serverUrl);
        }

        public async Task<string?> RegisterAgentAsync(RegisterAgentRequest request)
        {
            try
            {
                _logger.LogInformation("Wysyłam żądanie rejestracji do {BaseAddress}...", _httpClient.BaseAddress);

                var response = await _httpClient.PostAsJsonAsync("api/agents/register", request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RegisterAgentResponse>();
                    _logger.LogInformation("Rejestracja udana! DeviceID: {DeviceId}", result?.DeviceId);
                    return result?.Token;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Rejestracja nieudana. Status: {StatusCode}. Błąd: {Error}", 
                        response.StatusCode, error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie można połączyć się z serwerem.");
                return null;
            }
        }
    }
}