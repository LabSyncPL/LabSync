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

                var options = ParseOptions(parameters);
                var metrics = await CollectSystemMetricsAsync(options, cancellationToken);

                _logger?.LogDebug("Successfully collected system metrics.");

                return ModuleResult.Success(metrics);
            }
            catch (ArgumentException argEx)
            {
                _logger?.LogWarning(argEx, "Invalid arguments provided.");
                return ModuleResult.Failure(argEx.Message);
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

        private MetricsOptions ParseOptions(IDictionary<string, string> parameters)
        {
            // If no parameters or only command, return default (All)
            if (parameters.Count <= 1 && parameters.ContainsKey("__Command"))
            {
                return MetricsOptions.All;
            }          
            
            var options = new MetricsOptions();
            bool specificFlagFound = false;

            // Helper to check for flag presence
            bool HasFlag(string shortFlag, string longFlag)
            {
                return parameters.ContainsKey(shortFlag) || parameters.ContainsKey(longFlag);
            }

            // Validate if there are any unknown flags starting with '-'
            foreach (var key in parameters.Keys)
            {
                if (key.StartsWith("-") && key != "__Command")
                {
                    if (key != "-a" && key != "--all" &&
                        key != "-c" && key != "--cpu" &&
                        key != "-m" && key != "--memory" &&
                        key != "-d" && key != "--disks" &&
                        key != "-n" && key != "--network" &&
                        key != "-p" && key != "--processes" &&
                        key != "-s" && key != "--system")
                    {
                        throw new ArgumentException($"Unknown flag: {key}");
                    }
                }
            }

            if (HasFlag("-a", "--all"))
            {
                return MetricsOptions.All;
            }

            if (HasFlag("-c", "--cpu"))
            {
                options.CollectCpu = true;
                specificFlagFound = true;
            }

            if (HasFlag("-m", "--memory"))
            {
                options.CollectMemory = true;
                specificFlagFound = true;
            }

            if (HasFlag("-d", "--disks"))
            {
                options.CollectDisks = true;
                specificFlagFound = true;
            }

            if (HasFlag("-n", "--network"))
            {
                options.CollectNetwork = true;
                specificFlagFound = true;
            }
            
            if (HasFlag("-s", "--system"))
            {
                options.CollectSystemInfo = true;
                specificFlagFound = true;
            }
            
            // Processes not yet implemented in interfaces but requested in requirements
            if (HasFlag("-p", "--processes"))
            {
                options.CollectProcesses = true;
                specificFlagFound = true;
            }

            // If no specific flags were found, default to All (backward compatibility)
            if (!specificFlagFound)
            {
                return MetricsOptions.All;
            }

            return options;
        }

        private async Task<SystemMetrics> CollectSystemMetricsAsync(MetricsOptions options, CancellationToken cancellationToken)
        {
            if (_cpuInfoService == null ||
                _memoryInfoService == null ||
                _diskInfoService == null ||
                _networkInfoService == null ||
                _systemInfoService == null)
            {
                throw new InvalidOperationException("SystemInfoModule has not been initialized.");
            }

            var tasks = new List<Task>();
            Task<double>? cpuTask = null;
            Task<MemoryInfo>? memoryTask = null;
            Task<DiskInfo>? diskTask = null;
            Task<NetworkInfo>? networkTask = null;
            Task<SystemDetails>? systemTask = null;

            if (options.CollectCpu)
            {
                cpuTask = Task.Run(_cpuInfoService.GetCpuUsage, cancellationToken);
                tasks.Add(cpuTask);
            }

            if (options.CollectMemory)
            {
                memoryTask = Task.Run(_memoryInfoService.GetMemoryInfo, cancellationToken);
                tasks.Add(memoryTask);
            }

            if (options.CollectDisks)
            {
                diskTask = Task.Run(_diskInfoService.GetDiskInfo, cancellationToken);
                tasks.Add(diskTask);
            }

            if (options.CollectNetwork)
            {
                networkTask = Task.Run(_networkInfoService.GetNetworkInfo, cancellationToken);
                tasks.Add(networkTask);
            }

            if (options.CollectSystemInfo)
            {
                systemTask = Task.Run(_systemInfoService.GetSystemDetails, cancellationToken);
                tasks.Add(systemTask);
            }

            await Task.WhenAll(tasks);

            return new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuLoad = cpuTask?.Result,
                MemoryInfo = memoryTask?.Result,
                DiskInfo = diskTask?.Result,
                NetworkInfo = networkTask?.Result,
                SystemInfo = systemTask?.Result
            };
        }
    }
}
