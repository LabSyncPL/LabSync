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
            dto.RunAt,
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
            dto.RunAt,
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
            // One-time execution
            if (script.RunAt > now && (script.LastRunAt == null || script.LastRunAt < script.RunAt))
            {
                script.SetNextRunAt(script.RunAt);
            }
            else
            {
                script.SetNextRunAt(null);
            }
        }
        else if (!string.IsNullOrEmpty(script.CronExpression))
        {
            try
            {
                var cronExpression = CronExpression.Parse(script.CronExpression);
                
                // Get next occurrence using server's local time zone as requested
                var next = cronExpression.GetNextOccurrence(now, TimeZoneInfo.Local);
                script.SetNextRunAt(next);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse cron expression '{Cron}' for script {ScriptId}", script.CronExpression, script.Id);
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
