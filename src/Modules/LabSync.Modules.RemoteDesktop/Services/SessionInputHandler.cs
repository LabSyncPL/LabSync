using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.RemoteDesktop.Services;

public interface ISessionInputHandler
{
    Task RunInputLoopAsync(
        Stream dataChannelStream,
        IInputInjectionService input,
        Func<EncoderOptions, Task> onConfigure,
        EncoderOptions initialOptions,
        CancellationToken cancellationToken);
}

public class SessionInputHandler : ISessionInputHandler
{
    private readonly ILogger<SessionInputHandler> _logger;

    public SessionInputHandler(ILogger<SessionInputHandler> logger)
    {
        _logger = logger;
    }

    public async Task RunInputLoopAsync(
        Stream dataChannelStream,
        IInputInjectionService input,
        Func<EncoderOptions, Task> onConfigure,
        EncoderOptions initialOptions,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var currentOptions = initialOptions;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await dataChannelStream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;

                // Parse message
                var json = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                ControlMessage? message = null;
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    options.Converters.Add(new JsonStringEnumConverter());
                    message = JsonSerializer.Deserialize<ControlMessage>(json, options);
                }
                catch { /* Ignore malformed JSON */ }

                if (message != null && !string.IsNullOrWhiteSpace(message.Type))
                {
                    if (message.Type.Equals("configure", StringComparison.OrdinalIgnoreCase))
                    {
                        // Temporary fix: Block reconfiguration on Linux to prevent instability and "green screen" artifacts
                        // caused by switching to hardware encoders that might be misconfigured.
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            _logger.LogWarning("Ignoring 'configure' message on Linux to maintain stability. Reconfiguration disabled.");
                            continue;
                        }

                        // Handle Configuration
                        int newWidth = message.Width ?? currentOptions.OutputWidth;
                        int newHeight = message.Height ?? currentOptions.OutputHeight;
                        // Ensure even
                        if (newWidth % 2 != 0) newWidth--;
                        if (newHeight % 2 != 0) newHeight--;

                        var newOptions = currentOptions with
                        {
                            OutputWidth = newWidth,
                            OutputHeight = newHeight,
                            TargetBitrateKbps = message.BitrateKbps ?? currentOptions.TargetBitrateKbps,
                            TargetFps = message.Fps ?? currentOptions.TargetFps,
                            EncoderType = message.EncoderType ?? currentOptions.EncoderType
                        };

                        if (newOptions != currentOptions)
                        {
                            await onConfigure(newOptions);
                            currentOptions = newOptions;
                        }
                    }
                    else
                    {
                        // Handle Input
                        await InjectInputAsync(message, input, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task InjectInputAsync(ControlMessage message, IInputInjectionService input, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case "mouseMove" when message.X is not null && message.Y is not null:
                await input.InjectMouseMoveAsync(message.X.Value, message.Y.Value, cancellationToken);
                break;

            case "mouseButton" when message.Button is not null && message.Pressed is not null:
                if (Enum.TryParse<MouseButton>(message.Button, ignoreCase: true, out var button))
                {
                    await input.InjectMouseButtonAsync(button, message.Pressed.Value, cancellationToken);
                }
                break;

            case "mouseWheel" when message.DeltaX is not null && message.DeltaY is not null:
                await input.InjectMouseWheelAsync(message.DeltaX.Value, message.DeltaY.Value, cancellationToken);
                break;

            case "key" when message.KeyCode is not null && message.Pressed is not null:
                if (message.KeyCode.Value is >= 0 and <= 255)
                {
                    await input.InjectKeyAsync(message.KeyCode.Value, message.Pressed.Value, cancellationToken);
                }
                break;
        }
    }
}
