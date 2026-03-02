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

namespace LabSync.Agent.Modules.RemoteDesktop;

public class RemoteDesktopModule : IRemoteDesktopModule
{
    public string Name => "RemoteDesktop";
    public string Version => "1.0.0";

    private IRemoteSessionManager? _sessionManager;
    private ILogger? _logger;

    public Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger<RemoteDesktopModule>()
            : null;

        var hubInvoker = serviceProvider.GetService(typeof(LabSync.Core.Interfaces.IAgentHubInvoker)) as LabSync.Core.Interfaces.IAgentHubInvoker;
        if (hubInvoker == null)
        {
            _logger?.LogWarning("IAgentHubInvoker not registered. Remote desktop signaling will be unavailable.");
        }

        var loggerFactory = serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

        var signalingService = new RemoteDesktopSignalingService(
            hubInvoker ?? throw new InvalidOperationException("IAgentHubInvoker must be registered by the host for Remote Desktop module."),
            loggerFactory != null
                ? loggerFactory.CreateLogger<RemoteDesktopSignalingService>()
                : Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoteDesktopSignalingService>.Instance);

        var captureFactory = ScreenCaptureFactory.Create(serviceProvider);
        var inputFactory = InputInjectionFactory.Create(serviceProvider);

        var sipsLogger = loggerFactory != null
            ? loggerFactory.CreateLogger<SipsorceryWebRtcPeerConnectionService>()
            : Microsoft.Extensions.Logging.Abstractions.NullLogger<SipsorceryWebRtcPeerConnectionService>.Instance;
        IWebRtcPeerConnectionFactory peerFactory = new SipsorceryWebRtcPeerConnectionFactory(sipsLogger);
        _logger?.LogInformation("RemoteDesktop module configured to use SIPSorcery WebRTC peer connection. STUN server: stun:stun.l.google.com:19302.");

        _sessionManager = new RemoteSessionManager(
            signalingService,
            captureFactory,
            inputFactory,
            peerFactory,
            loggerFactory != null
                ? loggerFactory.CreateLogger<RemoteSessionManager>()
                : Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoteSessionManager>.Instance,
            SessionOptions.Default);

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
