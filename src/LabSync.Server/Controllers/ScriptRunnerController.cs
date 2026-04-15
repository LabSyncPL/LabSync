using System.Text.Json;
using System.Linq;
using LabSync.Core.Dto;
using LabSync.Core.Types;
using LabSync.Server.Data;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/script-runner")]
[Authorize(Policy = "RequireAdminRole")]
public sealed class ScriptRunnerController(
    JobDispatchService jobDispatch,
    ScriptTaskRegistry scriptTaskRegistry,
    LabSyncDbContext dbContext,
    ILogger<ScriptRunnerController> logger) : ControllerBase
{
    private const int MaxScriptContentChars = 200_000;
    private const int MaxArguments = 64;
    private const int MaxArgumentLength = 2_048;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 3_600;

    [HttpPost("execute")]
    public async Task<ActionResult<ExecuteScriptResponse>> Execute(
        [FromBody] ExecuteScriptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ScriptContent))
            return BadRequest(new ApiResponse("Script content is required."));

        if (request.TargetMachineIds is not { Length: > 0 })
            return BadRequest(new ApiResponse("At least one target machine is required."));

        if (!Enum.IsDefined(typeof(ScriptInterpreterType), request.InterpreterType))
            return BadRequest(new ApiResponse("Invalid interpreter type."));

        if (request.ScriptContent.Length > MaxScriptContentChars)
            return BadRequest(new ApiResponse($"Script content is too large (max {MaxScriptContentChars:N0} characters)."));

        if (request.TimeoutSeconds < MinTimeoutSeconds || request.TimeoutSeconds > MaxTimeoutSeconds)
            return BadRequest(new ApiResponse($"TimeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds}."));

        if (request.Arguments is { Length: > MaxArguments })
            return BadRequest(new ApiResponse($"A maximum of {MaxArguments} arguments is allowed."));

        if (request.Arguments is { Length: > 0 } && request.Arguments.Any(a => a.Length > MaxArgumentLength))
            return BadRequest(new ApiResponse($"Each argument must be at most {MaxArgumentLength:N0} characters."));

        var taskId = Guid.NewGuid();
        var warnings = new List<string>();

        foreach (var deviceId in request.TargetMachineIds)
        {
            var payload = JsonSerializer.Serialize(
                new
                {
                    taskId,
                    machineId = deviceId,
                    scriptContent = request.ScriptContent,
                    interpreterType = (int)request.InterpreterType,
                    arguments = request.Arguments,
                    timeoutSeconds = request.TimeoutSeconds,
                },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var job = await jobDispatch.DispatchAsync(
                deviceId,
                "ScriptExecution",
                "",
                payload,
                cancellationToken);

            if (job is null)
            {
                var msg = $"Device {deviceId:N} not found, not approved, or could not be dispatched.";
                warnings.Add(msg);
                logger.LogWarning("Script execute: {Message}", msg);
                continue;
            }

            scriptTaskRegistry.Register(taskId, deviceId, job.Id);
        }

        if (warnings.Count == request.TargetMachineIds.Length)
        {
            return BadRequest(new ApiResponse(
                string.Join(" ", warnings)));
        }

        return Ok(new ExecuteScriptResponse
        {
            JobId = taskId,
            DispatchWarnings = warnings.Count > 0 ? warnings.ToArray() : null,
        });
    }

    [HttpPost("cancel")]
    public async Task<ActionResult> Cancel([FromBody] CancelScriptTaskRequest request, CancellationToken cancellationToken)
    {
        if (request.TaskId == Guid.Empty)
            return BadRequest(new ApiResponse("TaskId is required."));

        var jobs = scriptTaskRegistry.GetJobs(request.TaskId);
        if (jobs.Count == 0)
            return NotFound(new ApiResponse("Unknown task or task has expired on the server."));

        IEnumerable<(Guid DeviceId, Guid JobId)> toCancel = jobs;
        if (request.MachineId is { } machineFilter && machineFilter != Guid.Empty)
            toCancel = jobs.Where(j => j.DeviceId == machineFilter).ToList();

        var cancelledAny = false;
        foreach (var (_, jobId) in toCancel)
        {
            var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is null)
                continue;

            if (job.Status == JobStatus.Pending || job.Status == JobStatus.Running)
            {
                job.Cancel();
                cancelledAny = true;
            }
        }

        if (cancelledAny)
            await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
