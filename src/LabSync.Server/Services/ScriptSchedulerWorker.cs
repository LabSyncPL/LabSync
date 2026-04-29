using System.Text.Json;
using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Core.Types;
using LabSync.Server.Data;
using LabSync.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Services;

public class ScriptSchedulerWorker(
    IServiceProvider serviceProvider,
    ILogger<ScriptSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Script Scheduler Worker is starting.");

        // Use a periodic timer for more reliable minute-based polling
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessDueScriptsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing scheduled scripts.");
            }
        }
    }

    private async Task ProcessDueScriptsAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LabSyncDbContext>();
        var schedulerService = scope.ServiceProvider.GetRequiredService<ScheduledScriptService>();
        var jobDispatch = scope.ServiceProvider.GetRequiredService<JobDispatchService>();
        var scriptTaskRegistry = scope.ServiceProvider.GetRequiredService<ScriptTaskRegistry>();

        var now = DateTimeOffset.UtcNow;
        var dueScripts = await dbContext.ScheduledScripts
            .Where(s => s.IsEnabled && s.NextRunAt != null && s.NextRunAt <= now)
            .ToListAsync(ct);

        if (dueScripts.Count == 0) return;

        logger.LogInformation("Found {Count} due scheduled scripts.", dueScripts.Count);

        foreach (var script in dueScripts)
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                // Re-fetch to ensure we have the latest state and can lock it (optimistic concurrency)
                var freshScript = await dbContext.ScheduledScripts
                    .FirstOrDefaultAsync(s => s.Id == script.Id && s.IsEnabled && s.NextRunAt != null && s.NextRunAt <= now, ct);

                if (freshScript == null) continue;

                await ExecuteScheduledScriptAsync(freshScript, dbContext, schedulerService, jobDispatch, scriptTaskRegistry, ct);
                
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute scheduled script {ScriptId}", script.Id);
                await transaction.RollbackAsync(ct);
            }
        }
    }

    private async Task ExecuteScheduledScriptAsync(
        ScheduledScript script,
        LabSyncDbContext dbContext,
        ScheduledScriptService schedulerService,
        JobDispatchService jobDispatch,
        ScriptTaskRegistry scriptTaskRegistry,
        CancellationToken ct)
    {
        logger.LogInformation("Executing scheduled script '{Name}' ({Id})", script.Name, script.Id);

        var taskId = Guid.NewGuid();
        var execution = new ScheduledScriptExecution(script.Id, taskId, script.NextRunAt ?? DateTimeOffset.UtcNow);
        dbContext.ScheduledScriptExecutions.Add(execution);

        // Determine target devices
        var targetDeviceIds = new List<Guid>();
        if (script.TargetType == ScheduledScriptTargetType.SingleAgent)
        {
            targetDeviceIds.Add(script.TargetId);
        }
        else if (script.TargetType == ScheduledScriptTargetType.Group)
        {
            // Resolve all approved devices in the group
            var groupDevices = await dbContext.Devices
                .Where(d => d.GroupId == script.TargetId && d.IsApproved)
                .Select(d => d.Id)
                .ToListAsync(ct);
            targetDeviceIds.AddRange(groupDevices);
            
            logger.LogInformation("Resolved {Count} devices for group target {GroupId}", groupDevices.Count, script.TargetId);
        }

        if (targetDeviceIds.Count == 0)
        {
            logger.LogWarning("No target devices found for scheduled script '{Name}' ({Id})", script.Name, script.Id);
            execution.MarkFailed("No target devices found.");
        }
        else
        {
            execution.MarkStarted();
            
            foreach (var deviceId in targetDeviceIds)
            {
                var payload = JsonSerializer.Serialize(
                    new
                    {
                        taskId,
                        machineId = deviceId,
                        scriptContent = script.ScriptContent,
                        interpreterType = (int)script.InterpreterType,
                        arguments = script.Arguments,
                        timeoutSeconds = script.TimeoutSeconds,
                    },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                var job = await jobDispatch.DispatchAsync(
                    deviceId,
                    "ScriptExecution",
                    "",
                    payload,
                    ct);

                if (job != null)
                {
                    scriptTaskRegistry.Register(taskId, deviceId, job.Id);
                }
                else
                {
                    logger.LogWarning("Failed to dispatch scheduled job to device {DeviceId} for script {ScriptId}", deviceId, script.Id);
                }
            }
            
            // For now, we mark the execution as completed once dispatched.
            // In a more complex system, we might wait for all jobs to finish or update this status based on job events.
            execution.MarkCompleted();
        }

        script.MarkRun(DateTimeOffset.UtcNow);
        schedulerService.CalculateNextRun(script);
    }
}
