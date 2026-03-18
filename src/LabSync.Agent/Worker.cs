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

    // Dodajemy Token, który pos³u¿y nam do wyzwalania restartu "od œrodka"
    private CancellationTokenSource _restartCts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LabSync Agent starting up...");

        // Load all available plugins from the 'Modules' directory.
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

        // PÊTLA RESTARTU - Agent bêdzie tu wraca³ za ka¿dym razem, gdy anulujesz _restartCts
        while (!stoppingToken.IsCancellationRequested)
        {
            // £¹czymy g³ówny token aplikacji z naszym tokenem restartu
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _restartCts.Token);
            var internalToken = linkedCts.Token;

            try
            {
                var agentIdentity = identityService.CollectIdentity();
                string? token = null;

                // Pêtla rejestracji - u¿ywa internalToken
                while (token == null && !internalToken.IsCancellationRequested)
                {
                    token = await serverClient.RegisterAgentAsync(agentIdentity);
                    if (token == null)
                    {
                        logger.LogWarning("Agent is not yet approved. Retrying in 30 seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(30), internalToken);
                    }
                }

                if (internalToken.IsCancellationRequested) continue; // Przerwie i przejdzie do catch/finally

                logger.LogInformation("Agent authorized successfully. Connecting to real-time service...");

                serverClient.OnReceiveJob += HandleJobAsync;

                await serverClient.ConnectAsync(token!, internalToken);

                var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(internalToken);
                var heartbeatTask = RunHeartbeatLoopAsync(heartbeatCts.Token);

                // Czekamy w nieskoñczonoœæ (a¿ do anulowania internalToken)
                await Task.Delay(Timeout.Infinite, internalToken);
            }
            catch (OperationCanceledException) when (_restartCts.IsCancellationRequested)
            {
                // To jest Oczekiwany b³¹d przy restarcie
                logger.LogInformation("Agent is restarting gracefully...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // To jest Oczekiwany b³¹d przy zamykaniu us³ugi
                logger.LogInformation("Agent is shutting down permanently.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "A fatal error occurred in the agent's main loop. Attempting to restart...");
                // Dodajemy ma³y delay, ¿eby nie spamowaæ serwera w razie b³êdu pêtli (np. brak neta)
                await Task.Delay(5000, stoppingToken);
            }
            finally
            {
                // Sprz¹tanie po obecnej iteracji
                serverClient.OnReceiveJob -= HandleJobAsync;
                await serverClient.DisposeAsync();
                _restartCts = new CancellationTokenSource();
            }
        }
    }

    private async void HandleJobAsync(Guid jobId, string command, string arguments, string? scriptPayload)
    {
        logger.LogInformation("DEBUG: Otrzymano komendê: {Cmd}", command);
        // Obs³uga wbudowanej komendy restartu
        if (command.Contains("Restart", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Received Job: RestartAgent. Initiating shutdown and restart sequence...");

            // Zg³aszamy sukces ZANIM zgasimy agenta
            await serverClient.ReportJobResultAsync(new JobResultDto(jobId, 0, "Restart sequence initiated..."));

            // Dajemy mu chwilê na wys³anie odpowiedzi do serwera, a nastêpnie triggerujemy restart
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                _restartCts.Cancel(); // To przerwie "internalToken" w ExecuteAsync
            });

            return;
        }

        logger.LogInformation("Received job {JobId}: Command='{Command}'", jobId, command);

        var module = moduleLoader.FindModuleForJob(command);
        if (module == null)
        {
            logger.LogError("No module found to handle command '{Command}'.", command);
            await serverClient.ReportJobResultAsync(new JobResultDto(jobId, -1, $"Error: No module found to handle command '{command}'."));
            return;
        }

        var parameters = ParseArguments(arguments);
        parameters["__Command"] = command;
        if (!string.IsNullOrWhiteSpace(scriptPayload))
        {
            parameters["ScriptPayload"] = scriptPayload;
        }

        using var cts = new CancellationTokenSource(DefaultJobTimeout);

        try
        {
            var moduleResult = await module.ExecuteAsync(parameters, cts.Token);

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
                logger.LogDebug("Arguments are not valid JSON, trying key=value format.");
            }
        }

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