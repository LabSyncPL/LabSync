using LabSync.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LabSync.Modules.SystemInfo
{
    public class SystemInfoModule : IAgentModule
    {
        public string Name => "SystemMonitor";
        public string Version => "1.0.0";

        private ILogger? _logger;

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var loggerFactory = (ILoggerFactory?)serviceProvider.GetService(typeof(ILoggerFactory));
            _logger = loggerFactory?.CreateLogger<SystemInfoModule>();

            _logger?.LogDebug($"Module {Name} initialized.");
            return Task.CompletedTask;
        }

        public bool CanHandle(string jobType)
        {
            return jobType.Equals("CollectMetrics", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Collecting system metrics...");

                await Task.Delay(100, cancellationToken);
                var metrics = new
                {
                    Description     = "Tmp data",
                    Type            = "PerformanceMetrics",
                    CpuLoad         = 15.5,             
                    RamAvailableMB  = 8192,      
                    DiskFreeSpaceGB = 250,
                    Timestamp       = DateTime.UtcNow
                };

                return ModuleResult.Success(metrics);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Operation cancelled.");
                return ModuleResult.Failure("Cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error collecting metrics");
                return ModuleResult.Failure(ex.Message);
            }
        }
    }
}