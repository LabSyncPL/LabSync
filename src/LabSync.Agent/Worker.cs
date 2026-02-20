using LabSync.Agent.Services;
using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using System.Text.Json;

namespace LabSync.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AgentIdentityService _identityService;
        private readonly ServerClient _serverClient;
        private readonly ModuleLoader _moduleLoader;
        private static readonly TimeSpan DefaultJobTimeout = TimeSpan.FromMinutes(30);

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
            
            var loadedModules = _moduleLoader.LoadedModules;
            if (loadedModules.Count == 0)
            {
                _logger.LogWarning("No modules loaded. Agent will not be able to execute jobs.");
            }
            else
            {
                _logger.LogInformation("Loaded {Count} module(s): {Modules}", 
                    loadedModules.Count, 
                    string.Join(", ", loadedModules.Select(m => $"{m.Module.Name} v{m.Module.Version}")));
            }

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
            _logger.LogInformation("Received job {JobId}: Command='{Command}', Arguments='{Arguments}'", 
                jobId, command, arguments);

            var module = _moduleLoader.FindModuleForJob(command);
            if (module == null)
            {
                _logger.LogError("No module found that can handle command '{Command}'. Available modules: {Modules}", 
                    command, string.Join(", ", _moduleLoader.LoadedModules.Select(m => m.Module.Name)));
                
                await _serverClient.ReportJobResultAsync(new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = -1,
                    Output = $"Error: No module found to handle command '{command}'. Available modules: {string.Join(", ", _moduleLoader.LoadedModules.Select(m => m.Module.Name))}"
                });
                return;
            }

            // Parse arguments into dictionary
            var parameters = ParseArguments(arguments);

            // Create cancellation token with timeout
            using var cts = new CancellationTokenSource(DefaultJobTimeout);
            var cancellationToken = cts.Token;

            try
            {
                _logger.LogInformation("Executing job {JobId} using module '{ModuleName}' v{Version}", 
                    jobId, module.Name, module.Version);

                var moduleResult = await module.ExecuteAsync(parameters, cancellationToken);

                // Serialize result data to JSON if it's an object
                string output;
                if (moduleResult.Data != null)
                {
                    if (moduleResult.Data is string str)
                    {
                        output = str;
                    }
                    else
                    {
                        try
                        {
                            output = JsonSerializer.Serialize(moduleResult.Data, new JsonSerializerOptions 
                            { 
                                WriteIndented = true 
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to serialize module result data. Using ToString().");
                            output = moduleResult.Data.ToString() ?? string.Empty;
                        }
                    }
                }
                else
                {
                    output = moduleResult.IsSuccess ? "Job completed successfully." : moduleResult.ErrorMessage ?? "Unknown error.";
                }

                var jobResult = new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = moduleResult.IsSuccess ? 0 : -1,
                    Output = output
                };

                await _serverClient.ReportJobResultAsync(jobResult);
                
                _logger.LogInformation("Successfully executed job {JobId} using module '{ModuleName}'. Success: {Success}", 
                    jobId, module.Name, moduleResult.IsSuccess);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Job {JobId} was cancelled or timed out.", jobId);
                await _serverClient.ReportJobResultAsync(new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = -1,
                    Output = "Job was cancelled or timed out."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while executing job {JobId} with module '{ModuleName}'.", 
                    jobId, module.Name);
                
                await _serverClient.ReportJobResultAsync(new JobResultDto
                {
                    JobId = jobId,
                    ExitCode = -1,
                    Output = $"Unhandled exception in module '{module.Name}': {ex.Message}\nStack trace: {ex.StackTrace}"
                });
            }
        }

        /// <summary>
        /// Parses job arguments string into a dictionary.
        /// Supports JSON format: {"key1":"value1","key2":"value2"}
        /// Or key=value format: key1=value1 key2=value2
        /// </summary>
        private Dictionary<string, string> ParseArguments(string arguments)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(arguments))
            {
                return parameters;
            }

            // Try JSON format first
            if (arguments.TrimStart().StartsWith('{'))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(arguments);
                    foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                    {
                        parameters[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                    return parameters;
                }
                catch (JsonException)
                {
                    // Fall through to key=value parsing
                    _logger.LogDebug("Arguments are not valid JSON, trying key=value format.");
                }
            }

            // Parse key=value format
            var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var equalIndex = part.IndexOf('=');
                if (equalIndex > 0 && equalIndex < part.Length - 1)
                {
                    var key = part.Substring(0, equalIndex).Trim();
                    var value = part.Substring(equalIndex + 1).Trim().Trim('"', '\'');
                    parameters[key] = value;
                }
                else if (!string.IsNullOrWhiteSpace(part))
                {
                    // If no '=' found, treat as a positional argument
                    parameters[$"arg{parameters.Count}"] = part;
                }
            }

            return parameters;
        }
    }
}
