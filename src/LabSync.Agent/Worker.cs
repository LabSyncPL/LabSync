using LabSync.Agent.Services;
using LabSync.Core.Dto;
using LabSync.Core.Interfaces;

namespace LabSync.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AgentIdentityService _identityService;
        private readonly ServerClient _serverClient;
        private readonly ModuleLoader _moduleLoader;

        public Worker(
            ILogger<Worker> logger,
            AgentIdentityService identityService,
            ServerClient serverClient,
            ModuleLoader moduleLoader)
        {
            _logger = logger;
            _identityService = identityService;
            _serverClient = serverClient;
            _moduleLoader = moduleLoader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LabSync Agent starting up...");

            // 1. Load all available plugins from the 'Modules' directory.
            await _moduleLoader.LoadPluginsAsync();

            // 2. Collect system identity to prepare for registration.
            var agentIdentity = _identityService.CollectIdentity();

            // 3. Loop until the agent is registered and approved by the server.
            string? token = null;
            while (token == null && !stoppingToken.IsCancellationRequested)
            {
                token = await _serverClient.RegisterAgentAsync(agentIdentity);
                if (token == null)
                {
                    _logger.LogWarning("Agent is not yet approved. Retrying in 30 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            if (stoppingToken.IsCancellationRequested) return;

            _logger.LogInformation("Agent authorized successfully. Connecting to real-time service...");

            // 4. Subscribe to the job receiver event before connecting.
            _serverClient.OnReceiveJob += HandleJobAsync;

            try
            {
                await _serverClient.ConnectAsync(token!, stoppingToken);

                var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var heartbeatTask = RunHeartbeatLoopAsync(heartbeatCts.Token);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }

                await heartbeatCts.CancelAsync();
                try { await heartbeatTask; } catch (OperationCanceledException) { }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent is shutting down.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "A fatal error occurred in the agent's main loop.");
            }
            finally
            {
                _serverClient.OnReceiveJob -= HandleJobAsync;
            }
        }

        private async Task RunHeartbeatLoopAsync(CancellationToken stoppingToken)
        {
            const int intervalSeconds = 30;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                await _serverClient.SendHeartbeatAsync(stoppingToken);
            }
        }

        private async void HandleJobAsync(Guid jobId, string command, string arguments)
        {
            _logger.LogInformation("Handling job {JobId} for command '{Command}'.", jobId, command);

            var module = _moduleLoader.FindModuleForJob(command);
            if (module == null)
            {
                _logger.LogError("No module found that can handle command '{Command}'.", command);
                await _serverClient.ReportJobResultAsync(new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = -1,
                    Output = $"Error: No module found to handle '{command}'."
                });
                return;
            }

            try
            {
                // Note: In a real implementation, 'arguments' would be parsed into a dictionary.
                // For now, we pass an empty dictionary.
                var moduleResult = await module.ExecuteAsync(new Dictionary<string, string>(), CancellationToken.None);

                var jobResult = new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = moduleResult.IsSuccess ? 0 : -1,
                    Output = moduleResult.IsSuccess ? moduleResult.Data?.ToString() : moduleResult.ErrorMessage
                };

                await _serverClient.ReportJobResultAsync(jobResult);
                _logger.LogInformation("Successfully executed job {JobId} and reported result.", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while executing job {JobId}.", jobId);
                await _serverClient.ReportJobResultAsync(new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = -1,
                    Output = $"Unhandled exception in module '{module.Name}': {ex.Message}"
                });
            }
        }
    }
}