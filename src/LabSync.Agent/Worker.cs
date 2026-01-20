using LabSync.Agent.Services;
using LabSync.Core.Interfaces;

namespace LabSync.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker>   _logger;
        private readonly AgentIdentityService _systemInfo;
        private readonly ServerClient      _serverClient;

        private readonly ModuleLoader _moduleLoader;

        public Worker(
            ILogger<Worker>   logger,
            AgentIdentityService systemInfo,
            ServerClient      serverClient,
            ModuleLoader      moduleLoader)
        {
            _logger       = logger;
            _systemInfo   = systemInfo;
            _serverClient = serverClient;
            _moduleLoader = moduleLoader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Agent Service started.");

            try
            {
                _moduleLoader.LoadPlugins();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error loading plugins!");
            }

            var systemData = _systemInfo.CollectIdentity();
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
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                /// for now it's temporary, in the future we will wait for a signal to perform
                /// some operation and then we will find the appropriate module and perform it
                _logger.LogInformation("Heartbeat sent.");

                var monitorModule = _moduleLoader.FindModuleForJob("CollectMetrics");
                if (monitorModule != null)
                {
                    try
                    {
                        var result = await monitorModule.ExecuteAsync(new Dictionary<string, string>(), stoppingToken);

                        if (result.IsSuccess)
                        {
                            _logger.LogInformation("MODULE OUTPUT: {Data}", result.Data);
                        }
                        else
                        {
                            _logger.LogError("Module failed: {Error}", result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing module");
                    }
                }
                else
                {
                    _logger.LogWarning("No plugin found capable of handling 'CollectMetrics'.");
                }

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}