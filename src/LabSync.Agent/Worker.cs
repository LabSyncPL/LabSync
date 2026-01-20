using LabSync.Agent.Services;
using LabSync.Core.Interfaces;

namespace LabSync.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker>      _logger;
        private readonly AgentIdentityService _systemInfo;
        private readonly ServerClient         _serverClient;

        private readonly ModuleLoader _moduleLoader;

        public Worker(
            ILogger<Worker>      logger,
            AgentIdentityService systemInfo,
            ServerClient         serverClient,
            ModuleLoader         moduleLoader)
        {
            _logger       = logger;
            _systemInfo   = systemInfo;
            _serverClient = serverClient;
            _moduleLoader = moduleLoader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            InitializeModules();
            var systemData = _systemInfo.CollectIdentity();

            string? token = null;
            while (token == null && !stoppingToken.IsCancellationRequested)
            {
                token = await _serverClient.RegisterAgentAsync(systemData);
                if (token == null)
                {
                    _logger.LogWarning("Waiting for Administrator approval.");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            if (token != null)
            {
                _logger.LogInformation("Agent authorized successfully.");
                await RunMainLoopAsync(token, stoppingToken);
            }
        }

        private async Task RunMainLoopAsync(string token, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                /// For now it's temporary, in the future we will wait for a signal to perform
                /// some operation and then we will find the appropriate module and perform it

                _logger.LogInformation("Heartbeat sent.");

                var monitorModule = _moduleLoader.FindModuleForJob("CollectMetrics");
                if (monitorModule != null)
                {
                    try
                    {
                        var result = await monitorModule.ExecuteAsync(new Dictionary<string, string>(), stoppingToken);
                        if (result.IsSuccess)
                            _logger.LogInformation("MODULE OUTPUT: {Data}", result.Data);
                        else
                            _logger.LogError("Module failed: {Error}", result.ErrorMessage);
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

        private void InitializeModules()
        {
            try
            {
                _moduleLoader.LoadPlugins();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error loading plugins!");
            }
        }
    }
}