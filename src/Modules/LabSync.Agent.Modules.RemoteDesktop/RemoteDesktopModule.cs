using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LabSync.Agent.Modules.RemoteDesktop.Abstractions;
using LabSync.Agent.Modules.RemoteDesktop.Capture;
using LabSync.Agent.Modules.RemoteDesktop.Infrastructure;
using LabSync.Agent.Modules.RemoteDesktop.Input;
using LabSync.Agent.Modules.RemoteDesktop.Models;
using LabSync.Agent.Modules.RemoteDesktop.Services;
using LabSync.Agent.Modules.RemoteDesktop.WebRtc;
using LabSync.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using LabSync.Agent.Modules.RemoteDesktop.Configuration;

namespace LabSync.Agent.Modules.RemoteDesktop;

public class RemoteDesktopModule : IRemoteDesktopModule
{
    public string Name => "RemoteDesktop";
    public string Version => "1.0.0";

    private IRemoteSessionManager? _sessionManager;
    private GridMonitorService? _gridMonitorService;
    private ILogger? _logger;
    private IServiceProvider? _moduleServiceProvider;

    public Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<RemoteDesktopModule>();

        var hubInvoker = serviceProvider.GetService<IAgentHubInvoker>();
        if (hubInvoker == null)
        {
            _logger?.LogWarning("IAgentHubInvoker not registered. Remote desktop signaling will be unavailable.");
            return Task.CompletedTask;
        }

        var services = new ServiceCollection();

        // Host services
        services.AddSingleton(hubInvoker);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        if (serviceProvider.GetService<IAgentContext>() is { } agentContext)
        {
            services.AddSingleton(agentContext);
        }

        // Configuration
        var configuration = serviceProvider.GetService<IConfiguration>();
        if (configuration != null)
        {
            services.Configure<WebRtcConfiguration>(options => configuration.GetSection(WebRtcConfiguration.SectionName).Bind(options));
        }
        else
        {
            _logger?.LogWarning("IConfiguration not available. Using default WebRTC settings.");
            services.Configure<WebRtcConfiguration>(options => { });
        }

        // Module specific services
        services.AddSingleton<IRemoteDesktopSignalingService, RemoteDesktopSignalingService>();
        services.AddSingleton<IScreenCaptureFactory, ScreenCaptureFactorySelector>();
        services.AddSingleton<IInputInjectionFactory>(sp => InputInjectionFactory.Create(sp));
        services.AddSingleton<IWebRtcPeerConnectionFactory, SipsorceryWebRtcPeerConnectionFactory>();
        services.AddSingleton<IGpuDiscoveryService, GpuDiscoveryService>();
        services.AddSingleton<IVideoEncoderFactory, VideoEncoderFactory>();
        services.AddSingleton<ISessionInputHandler, SessionInputHandler>();
        services.AddSingleton<ICaptureSession, CaptureSession>();
        services.AddSingleton<IRemoteSessionManager, RemoteSessionManager>();
        services.AddSingleton<GridMonitorService>();

        _moduleServiceProvider = services.BuildServiceProvider();

        _gridMonitorService = _moduleServiceProvider.GetRequiredService<GridMonitorService>();
        _sessionManager = _moduleServiceProvider.GetRequiredService<IRemoteSessionManager>();

        var signalingService = _moduleServiceProvider.GetRequiredService<IRemoteDesktopSignalingService>();
        var resolvedAgentContext = _moduleServiceProvider.GetService<IAgentContext>();

        signalingService.OnStartSessionRequested += (sessionId, prefs) =>
        {
            if (resolvedAgentContext != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var deviceId = resolvedAgentContext.DeviceId;
                        if (deviceId == Guid.Empty)
                        {
                            _logger?.LogWarning("AgentContext returned empty DeviceId for session {SessionId}. Aborting session start.", sessionId);
                            return;
                        }

                        await _sessionManager.StartSessionAsync(new StartSessionRequest(deviceId, null, sessionId, prefs));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to start session from SignalR request. SessionId: {SessionId}", sessionId);
                    }
                });
            }
            else
            {
                _logger?.LogWarning("Cannot start session {SessionId} because AgentContext is not available (DeviceId unknown).", sessionId);
            }
        };

        signalingService.OnStopSessionRequested += (sessionId) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger?.LogInformation("Stopping session {SessionId} from SignalR request.", sessionId);
                    await _sessionManager.StopSessionAsync(sessionId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to stop session from SignalR request. SessionId: {SessionId}", sessionId);
                }
            });
        };

        _logger?.LogInformation("RemoteDesktop module initialized. Platform: {Platform}", RuntimeInformation.OSDescription);
        return Task.CompletedTask;
    }

    public bool CanHandle(string jobType)
    {
        return string.Equals(jobType, "StartRemoteDesktop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(jobType, "StopRemoteDesktop", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        if (_sessionManager == null)
            return ModuleResult.Failure("RemoteDesktop module not initialized.");

        if (parameters.TryGetValue("SessionId", out var sid) || parameters.TryGetValue("sessionId", out sid))
        {
            var sessionIdStr = sid ?? "";
            if (!Guid.TryParse(sessionIdStr, out var sessionId))
                return ModuleResult.Failure("StopRemoteDesktop requires a valid SessionId.");
            await _sessionManager.StopSessionAsync(sessionId, cancellationToken);
            return ModuleResult.Success(new { SessionId = sessionId, Stopped = true });
        }

        var deviceIdStr = parameters.TryGetValue("DeviceId", out var did) ? did : (parameters.TryGetValue("deviceId", out did) ? did : "");
        if (!Guid.TryParse(deviceIdStr, out var deviceId))
            return ModuleResult.Failure("StartRemoteDesktop requires a valid DeviceId.");

        var requestedBy = parameters.TryGetValue("RequestedByUserId", out var rbu) ? rbu : (parameters.TryGetValue("requestedByUserId", out rbu) ? rbu : null);
        var result = await _sessionManager.StartSessionAsync(
            new StartSessionRequest(deviceId, requestedBy),
            cancellationToken);

        if (!result.Success)
            return ModuleResult.Failure(result.ErrorMessage ?? "Failed to start session.");

        return ModuleResult.Success(new { result.SessionId, Started = true });
    }
}
