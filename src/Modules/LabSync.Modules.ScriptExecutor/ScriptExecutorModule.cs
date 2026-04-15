using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using LabSync.Core.Dto;
using LabSync.Core.Interfaces;
using LabSync.Modules.ScriptExecutor.Interpreters;
using LabSync.Modules.ScriptExecutor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.ScriptExecutor;

public sealed class ScriptExecutorModule : IAgentModule
{
    private const int TelemetryChannelCapacity = 2048;
    public string Name => "ScriptExecutor";
    public string Version => "1.0.0";

    /// <summary>
    /// LabSync standard for decoded stdout/stderr lines (matches UTF-8 script files and Windows preamble).
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private ILogger<ScriptExecutorModule>? _logger;
    private IAgentHubInvoker? _hubInvoker;

    private readonly IReadOnlyDictionary<InterpreterType, IScriptInterpreter> _interpreters =
        new Dictionary<InterpreterType, IScriptInterpreter>
        {
            [InterpreterType.PowerShell] = new PowerShellInterpreter(),
            [InterpreterType.Bash] = new BashInterpreter(),
            [InterpreterType.Cmd] = new CmdInterpreter(),
        };

    public Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ScriptExecutorModule>();
        _hubInvoker = serviceProvider.GetService<IAgentHubInvoker>();
        _logger?.LogInformation("ScriptExecutor module initialized.");
        return Task.CompletedTask;
    }

    public bool CanHandle(string jobType)
    {
        return jobType.Equals("ScriptExecution", StringComparison.OrdinalIgnoreCase)
            || jobType.Equals("RunScript", StringComparison.OrdinalIgnoreCase)
            || jobType.Equals("ScriptExecutor", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = BuildEnvelope(parameters);
            var result = await HandleCommandAsync(envelope, cancellationToken);
            return ModuleResult.Success(result);
        }
        catch (OperationCanceledException)
        {
            return ModuleResult.Failure("Script execution was cancelled.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Script execution failed.");
            return ModuleResult.Failure(ex.Message);
        }
    }

    public async Task<ExecutionResult> HandleCommandAsync(CommandEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envelope.ScriptContent))
        {
            throw new ArgumentException("ScriptContent cannot be empty.");
        }

        if (!_interpreters.TryGetValue(envelope.InterpreterType, out var interpreter))
        {
            throw new NotSupportedException($"Interpreter '{envelope.InterpreterType}' is not registered.");
        }

        if (!interpreter.IsSupportedOnCurrentPlatform())
        {
            throw new PlatformNotSupportedException(
                $"Interpreter '{envelope.InterpreterType}' is not supported on current platform.");
        }

        var timeoutSeconds = envelope.TimeoutSeconds <= 0 ? 300 : envelope.TimeoutSeconds;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedToken = linkedCts.Token;

        var sw = Stopwatch.StartNew();
        var scriptForDisk = interpreter.ApplyEncodingPreamble(envelope.ScriptContent);
        var tempFile = CreateTemporaryScript(scriptForDisk, envelope.InterpreterType);

        try
        {
            var startInfo = interpreter.CreateStartInfo(tempFile, envelope.Arguments ?? []);
            ApplyLabSyncProcessStreamEncoding(startInfo);
            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start script process.");
            }

            var telemetryChannel = Channel.CreateBounded<ScriptOutputTelemetryDto>(new BoundedChannelOptions(TelemetryChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                telemetryChannel.Writer.TryWrite(new ScriptOutputTelemetryDto(
                    envelope.TaskId,
                    envelope.MachineId,
                    envelope.InterpreterType.ToString(),
                    "stdout",
                    e.Data,
                    DateTimeOffset.UtcNow));
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                telemetryChannel.Writer.TryWrite(new ScriptOutputTelemetryDto(
                    envelope.TaskId,
                    envelope.MachineId,
                    envelope.InterpreterType.ToString(),
                    "stderr",
                    e.Data,
                    DateTimeOffset.UtcNow));
            };

            var telemetryTask = PumpTelemetryAsync(telemetryChannel.Reader, CancellationToken.None);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(linkedToken);
            }
            catch (OperationCanceledException)
            {
                TryKillProcessTree(process);
                throw;
            }
            finally
            {
                telemetryChannel.Writer.TryComplete();
                await telemetryTask;
            }

            sw.Stop();
            var exitCode = process.ExitCode;
            var success = exitCode == 0;
            await PublishTaskCompletedAsync(envelope, exitCode, success, CancellationToken.None);

            _logger?.LogInformation(
                "Script finished. Interpreter={Interpreter}, ExitCode={ExitCode}, DurationMs={DurationMs}",
                envelope.InterpreterType, exitCode, sw.ElapsedMilliseconds);

            return new ExecutionResult(exitCode, sw.Elapsed, success);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            await PublishTaskCompletedAsync(envelope, -1, false, CancellationToken.None);
            sw.Stop();
            _logger?.LogWarning(
                "Script timed out after {TimeoutSeconds}s. Interpreter={Interpreter}",
                timeoutSeconds,
                envelope.InterpreterType);
            throw new TimeoutException($"Script execution exceeded timeout of {timeoutSeconds} seconds.");
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception)
        {
            await PublishTaskCompletedAsync(envelope, -1, false, CancellationToken.None);
            throw;
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private async Task PublishTaskCompletedAsync(
        CommandEnvelope envelope,
        int exitCode,
        bool isSuccess,
        CancellationToken cancellationToken)
    {
        if (envelope.TaskId is not { } taskId || taskId == Guid.Empty)
            return;
        if (envelope.MachineId is not { } machineId || machineId == Guid.Empty)
            return;

        try
        {
            if (_hubInvoker != null)
            {
                var dto = new ScriptTaskCompletedDto(taskId, machineId, exitCode, isSuccess);
                await _hubInvoker.InvokeAsync("ScriptTaskCompleted", [dto], cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to publish ScriptTaskCompleted.");
        }
    }

    private async Task PumpTelemetryAsync(ChannelReader<ScriptOutputTelemetryDto> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    if (_hubInvoker != null)
                    {
                        await _hubInvoker.InvokeAsync("ScriptOutputTelemetry", [item], cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to publish ScriptOutputTelemetry.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected on shutdown.
        }
    }

    private static string NormalizeLineEndings(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // For Windows, ensure all line endings are consistently CRLF
            // We first replace CRLF with LF to avoid double CRs, then replace all LF with CRLF
            return content.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        // For Linux/macOS, just ensure we use LF
        return content.Replace("\r\n", "\n");
    }

    private static string CreateTemporaryScript(string scriptContent, InterpreterType type)
    {
        var extension = type switch
        {
            InterpreterType.PowerShell => ".ps1",
            InterpreterType.Cmd => ".cmd",
            InterpreterType.Bash => ".sh",
            _ => ".txt"
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"labsync-script-{Guid.NewGuid():N}{extension}");
        
        var normalizedContent = NormalizeLineEndings(scriptContent);

        // UTF-8 without BOM: compatible with Bash on Linux and PowerShell reading .ps1 on Windows.
        File.WriteAllText(tempPath, normalizedContent, Utf8NoBom);
        return tempPath;
    }

    /// <summary>
    /// <see cref="Process.BeginOutputReadLine"/> decodes pipe bytes using these encodings; must match UTF-8 emitted after interpreter preambles on Windows.
    /// </summary>
    private static void ApplyLabSyncProcessStreamEncoding(ProcessStartInfo startInfo)
    {
        startInfo.StandardOutputEncoding = Utf8NoBom;
        startInfo.StandardErrorEncoding = Utf8NoBom;
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static CommandEnvelope BuildEnvelope(IDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("ScriptPayload", out var payload) && !string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<CommandEnvelope>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fallback to key/value parsing.
            }
        }

        var scriptContent = parameters.TryGetValue("ScriptContent", out var script) ? script : string.Empty;

        var typeRaw = parameters.TryGetValue("InterpreterType", out var t)
            ? t
            : (OperatingSystem.IsWindows() ? "PowerShell" : "Bash");

        if (!Enum.TryParse<InterpreterType>(typeRaw, ignoreCase: true, out var interpreterType))
        {
            throw new ArgumentException($"Unsupported InterpreterType '{typeRaw}'.");
        }

        var timeout = 300;
        if (parameters.TryGetValue("TimeoutSeconds", out var timeoutRaw) &&
            int.TryParse(timeoutRaw, out var parsedTimeout) &&
            parsedTimeout > 0)
        {
            timeout = parsedTimeout;
        }

        var args = Array.Empty<string>();
        if (parameters.TryGetValue("Arguments", out var argsRaw) && !string.IsNullOrWhiteSpace(argsRaw))
        {
            try
            {
                var parsedArgs = JsonSerializer.Deserialize<string[]>(argsRaw);
                if (parsedArgs != null)
                {
                    args = parsedArgs;
                }
            }
            catch (JsonException)
            {
                // Keep empty args if malformed.
            }
        }

        return new CommandEnvelope(scriptContent, interpreterType, args, timeout);
    }
}

