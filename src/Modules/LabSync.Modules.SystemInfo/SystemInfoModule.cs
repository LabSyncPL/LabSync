using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LabSync.Core.Interfaces;
using LabSync.Modules.SystemInfo.Interfaces;
using LabSync.Modules.SystemInfo.Models;
using Microsoft.Extensions.Logging;

namespace LabSync.Modules.SystemInfo
{
    public class SystemInfoModule : IAgentModule
    {
        public string Name => "SystemInfo";
        public string Version => "3.1.0";

        private ILogger? _logger;
        private ICpuInfoService? _cpuInfoService;
        private IMemoryInfoService? _memoryInfoService;
        private IDiskInfoService? _diskInfoService;
        private INetworkInfoService? _networkInfoService;
        private ISystemInfoService? _systemInfoService;
        private IHardwareInfoService? _hardwareInfoService;

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var loggerFactory = (ILoggerFactory?)serviceProvider.GetService(typeof(ILoggerFactory));
            _logger = loggerFactory?.CreateLogger<SystemInfoModule>();

            _cpuInfoService = new CpuInfoService(loggerFactory?.CreateLogger<CpuInfoService>());
            _memoryInfoService = new MemoryInfoService(loggerFactory?.CreateLogger<MemoryInfoService>());
            _diskInfoService = new DiskInfoService(loggerFactory?.CreateLogger<DiskInfoService>());
            _networkInfoService = new NetworkInfoService(loggerFactory?.CreateLogger<NetworkInfoService>());
            _systemInfoService = new SystemInfoService(loggerFactory?.CreateLogger<SystemInfoService>());
            _hardwareInfoService = new HardwareInfoService(loggerFactory?.CreateLogger<HardwareInfoService>());

            _logger?.LogInformation("SystemInfo module initialized. Platform: {Platform}",
                RuntimeInformation.OSDescription);

            return Task.CompletedTask;
        }

        public bool CanHandle(string jobType)
        {
            return jobType.Equals("CollectMetrics", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("Get-SysInfo", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("SystemInfo", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("Get-HardwareSpecs", StringComparison.OrdinalIgnoreCase) ||
                   jobType.Equals("HardwareSpecs", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            try
            {
                // Determine command
                string command = "CollectMetrics";
                if (parameters.TryGetValue("__Command", out var cmd))
                {
                    command = cmd;
                }

                if (command.Equals("Get-HardwareSpecs", StringComparison.OrdinalIgnoreCase) ||
                    command.Equals("HardwareSpecs", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExecuteHardwareSpecsAsync(cancellationToken);
                }

                // Default to metrics collection
                _logger?.LogInformation("Collecting system metrics...");

                var metrics = await CollectSystemMetricsAsync(cancellationToken);

                _logger?.LogDebug("Successfully collected system metrics. CPU: {Cpu}%, Memory: {Memory}%, Disk: {Disk}%, Network RX: {NetworkRx}",
                    metrics.CpuLoad, metrics.MemoryInfo.UsagePercent, metrics.DiskInfo.UsagePercent, metrics.NetworkInfo.TotalBytesReceivedPerSecond);

                return ModuleResult.Success(metrics);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Operation was cancelled.");
                return ModuleResult.Failure("Operation was cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing SystemInfo module");
                return ModuleResult.Failure($"Failed to execute: {ex.Message}");
            }
        }

        private Task<ModuleResult> ExecuteHardwareSpecsAsync(CancellationToken cancellationToken)
        {
            if (_hardwareInfoService == null)
            {
                return Task.FromResult(ModuleResult.Failure("HardwareInfoService not initialized."));
            }

            _logger?.LogInformation("Collecting hardware specs...");
            
            // This is usually fast enough to run synchronously, but wrapped in Task for consistency
            return Task.Run(() =>
            {
                try
                {
                    var specs = _hardwareInfoService.GetHardwareInfo();
                    _logger?.LogInformation("Successfully collected hardware specs");
                    return ModuleResult.Success(specs);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to collect hardware specs");
                    return ModuleResult.Failure($"Failed to collect hardware specs: {ex.Message}");
                }
            }, cancellationToken);
        }

        private async Task<SystemMetrics> CollectSystemMetricsAsync(CancellationToken cancellationToken)
        {
            if (_cpuInfoService == null ||
                _memoryInfoService == null ||
                _diskInfoService == null ||
                _networkInfoService == null ||
                _systemInfoService == null)
            {
                throw new InvalidOperationException("SystemInfoModule has not been initialized.");
            }

            var cpuTask = Task.Run(_cpuInfoService.GetCpuUsage, cancellationToken);
            var memoryTask = Task.Run(_memoryInfoService.GetMemoryInfo, cancellationToken);
            var diskTask = Task.Run(_diskInfoService.GetDiskInfo, cancellationToken);
            var networkTask = Task.Run(_networkInfoService.GetNetworkInfo, cancellationToken);

            await Task.WhenAll(cpuTask, memoryTask, diskTask, networkTask);

            return new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuLoad = cpuTask.Result,
                MemoryInfo = memoryTask.Result,
                DiskInfo = diskTask.Result,
                NetworkInfo = networkTask.Result
            };
        }
    }
}
