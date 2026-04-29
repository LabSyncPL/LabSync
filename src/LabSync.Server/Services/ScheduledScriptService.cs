using Cronos;
using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Services;

public class ScheduledScriptService(LabSyncDbContext dbContext, ILogger<ScheduledScriptService> logger)
{
    public async Task<ScheduledScriptDto> CreateAsync(CreateScheduledScriptDto dto, string? createdBy = null)
    {
        var script = new ScheduledScript(
            dto.Name,
            dto.ScriptContent,
            dto.InterpreterType,
            dto.Arguments,
            dto.TimeoutSeconds,
            dto.CronExpression,
            dto.RunAt?.ToUniversalTime(), // Ensure UTC
            dto.TargetType,
            dto.TargetId,
            createdBy);

        CalculateNextRun(script);

        dbContext.ScheduledScripts.Add(script);
        await dbContext.SaveChangesAsync();

        return MapToDto(script);
    }

    public async Task<ScheduledScriptDto?> UpdateAsync(Guid id, UpdateScheduledScriptDto dto)
    {
        var script = await dbContext.ScheduledScripts.FindAsync(id);
        if (script == null) return null;

        script.Update(
            dto.Name,
            dto.ScriptContent,
            dto.InterpreterType,
            dto.Arguments,
            dto.TimeoutSeconds,
            dto.CronExpression,
            dto.RunAt?.ToUniversalTime(), // Ensure UTC
            dto.TargetType,
            dto.TargetId);

        if (dto.IsEnabled) script.Enable(); else script.Disable();

        CalculateNextRun(script);

        await dbContext.SaveChangesAsync();
        return MapToDto(script);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var script = await dbContext.ScheduledScripts.FindAsync(id);
        if (script == null) return false;

        dbContext.ScheduledScripts.Remove(script);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<ScheduledScriptDto>> ListAsync()
    {
        var scripts = await dbContext.ScheduledScripts
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return scripts.Select(MapToDto).ToList();
    }

    public async Task<ScheduledScriptDto?> GetByIdAsync(Guid id)
    {
        var script = await dbContext.ScheduledScripts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        return script != null ? MapToDto(script) : null;
    }

    public void CalculateNextRun(ScheduledScript script)
    {
        if (!script.IsEnabled)
        {
            script.SetNextRunAt(null);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        if (script.RunAt.HasValue)
        {
            // One-time execution: only schedule if it's in the future and hasn't run yet
            if (script.RunAt > now && (script.LastRunAt == null || script.LastRunAt < script.RunAt))
            {
                script.SetNextRunAt(script.RunAt);
            }
            else
            {
                script.SetNextRunAt(null);
            }
        }
        else if (!string.IsNullOrWhiteSpace(script.CronExpression))
        {
            try
            {
                var expression = script.CronExpression.Trim();
                
                // Try to parse as Standard first, then with Seconds
                CronExpression? cronExpression = null;
                try
                {
                    cronExpression = CronExpression.Parse(expression, CronFormat.Standard);
                }
                catch
                {
                    try
                    {
                        cronExpression = CronExpression.Parse(expression, CronFormat.IncludeSeconds);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to parse cron expression '{Cron}' for script {ScriptId} in any supported format.", expression, script.Id);
                    }
                }

                if (cronExpression != null)
                {
                    // Get next occurrence strictly after 'now'
                    // Use server's local time, with a fallback to UTC if Local is problematic
                    TimeZoneInfo tz;
                    try { tz = TimeZoneInfo.Local; } catch { tz = TimeZoneInfo.Utc; }

                    var next = cronExpression.GetNextOccurrence(now, tz);
                    // Crucial: Npgsql requires UTC for 'timestamp with time zone'
                    script.SetNextRunAt(next?.ToUniversalTime());
                }
                else
                {
                    script.SetNextRunAt(null);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error calculating next run for script {ScriptId}", script.Id);
                script.SetNextRunAt(null);
            }
        }
        else
        {
            script.SetNextRunAt(null);
        }
    }

    private static ScheduledScriptDto MapToDto(ScheduledScript s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ScriptContent = s.ScriptContent,
        InterpreterType = s.InterpreterType,
        Arguments = s.Arguments,
        TimeoutSeconds = s.TimeoutSeconds,
        CronExpression = s.CronExpression,
        RunAt = s.RunAt,
        IsEnabled = s.IsEnabled,
        LastRunAt = s.LastRunAt,
        NextRunAt = s.NextRunAt,
        TargetType = s.TargetType,
        TargetId = s.TargetId,
        CreatedAt = s.CreatedAt
    };
}
