using LabSync.Agent.Services;

namespace LabSync.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly SystemInfoService _systemInfo;
        private readonly ServerClient _serverClient;

        public Worker(
            ILogger<Worker> logger,
            SystemInfoService systemInfo,
            ServerClient serverClient)
        {
            _logger = logger;
            _systemInfo = systemInfo;
            _serverClient = serverClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Agent Service started.");

            var systemData = _systemInfo.CollectSystemInfo();
            _logger.LogInformation("System Info Collected: {Hostname} ({Mac})", systemData.Hostname, systemData.MacAddress);

            string? token = null;
            while (token == null && !stoppingToken.IsCancellationRequested)
            {
                token = await _serverClient.RegisterAgentAsync(systemData);

                if (token == null)
                {
                    _logger.LogWarning("Server unavailable. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            if (token != null)
            {
                _logger.LogInformation("Agent authorized successfully. Ready for jobs.");
                // TODO: Save token to disk for future restarts
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Heartbeat...");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}