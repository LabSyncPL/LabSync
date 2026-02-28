using System.Text.Json;
using LabSync.Agent.Services;
using LabSync.Core.Dto;

namespace LabSync.Agent;

public class Worker(
    ILogger<Worker> logger,
    AgentIdentityService identityService,
    ServerClient serverClient,
    ModuleLoader moduleLoader) : BackgroundService
{
    private static readonly TimeSpan DefaultJobTimeout = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LabSync Agent starting up...");

        /// Load all available plugins from the 'Modules' directory.
        await moduleLoader.LoadPluginsAsync();

        var loadedModules = moduleLoader.LoadedModules;
        if (loadedModules.Count == 0)
        {
            logger.LogWarning("No modules loaded. Agent will not be able to execute jobs.");
        }
        else
        {
            logger.LogInformation("Loaded {Count} module(s): {Modules}",
                loadedModules.Count,
                string.Join(", ", loadedModules.Select(m => $"{m.Module.Name} v{m.Module.Version}")));
        }

        /// Collect system identity to prepare for registration.
        var agentIdentity = identityService.CollectIdentity();
        string? token = null;
        while (token == null && !stoppingToken.IsCancellationRequested)
        {
            token = await serverClient.RegisterAgentAsync(agentIdentity);
            if (token == null)
            {
                logger.LogWarning("Agent is not yet approved. Retrying in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested) return;

        logger.LogInformation("Agent authorized successfully. Connecting to real-time service...");

        /// Subscribe to the job receiver event before connecting.
        serverClient.OnReceiveJob += HandleJobAsync;

        try
        {
            await serverClient.ConnectAsync(token!, stoppingToken);

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
            logger.LogInformation("Agent is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "A fatal error occurred in the agent's main loop.");
        }
        finally
        {
            serverClient.OnReceiveJob -= HandleJobAsync;
        }
    }


    private async void HandleJobAsync(Guid jobId, string command, string arguments, string? scriptPayload)
    {
        logger.LogInformation("Received job {JobId}: Command='{Command}'", jobId, command);

        var module = moduleLoader.FindModuleForJob(command);
        if (module == null)
        {
            logger.LogError("No module found to handle command '{Command}'.", command);
            await serverClient.ReportJobResultAsync(new JobResultDto(jobId, -1, $"Error: No module found to handle command '{command}'."));
            return;
        }

        var parameters = ParseArguments(arguments);
        if (!string.IsNullOrWhiteSpace(scriptPayload))
        {
            parameters["ScriptPayload"] = scriptPayload;
        }

        using var cts = new CancellationTokenSource(DefaultJobTimeout);

        try
        {
            var moduleResult = await module.ExecuteAsync(parameters, cts.Token);

            /// Serialize result data to JSON if it's an object
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
                        logger.LogWarning(ex, "Failed to serialize module result data. Using ToString().");
                        output = moduleResult.Data.ToString() ?? string.Empty;
                    }
                }
            }
            else
            {
                output = moduleResult.IsSuccess ? "Job completed successfully." : moduleResult.ErrorMessage ?? "Unknown error.";
            }

            var jobResult = new JobResultDto(
                jobId,
                moduleResult.IsSuccess ? 0 : -1,
                output
            );

            await serverClient.ReportJobResultAsync(jobResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing job.");
            await serverClient.ReportJobResultAsync(new JobResultDto(jobId, -1, ex.Message));
        }
    }

    private Dictionary<string, string> ParseArguments(string arguments)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(arguments))
        {
            return parameters;
        }

        /// Try JSON format first
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
                /// Fall through to key=value parsing
                logger.LogDebug("Arguments are not valid JSON, trying key=value format.");
            }
        }

        /// Parse key=value format
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
                /// If no '=' found, treat as a positional argument
                parameters[$"arg{parameters.Count}"] = part;
            }
        }

        return parameters;
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
            await serverClient.SendHeartbeatAsync(stoppingToken);
        }
    }
}
