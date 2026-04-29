using LabSync.Core.Dto;
using LabSync.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/scheduled-scripts")]
[Authorize(Policy = "RequireAdminRole")]
public class ScriptSchedulerController(ScheduledScriptService schedulerService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ScheduledScriptDto>> Create([FromBody] CreateScheduledScriptDto dto)
    {
        var result = await schedulerService.CreateAsync(dto, User.Identity?.Name);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ScheduledScriptDto>> Update(Guid id, [FromBody] UpdateScheduledScriptDto dto)
    {
        var result = await schedulerService.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await schedulerService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<ScheduledScriptDto>>> List()
    {
        return Ok(await schedulerService.ListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScheduledScriptDto>> GetById(Guid id)
    {
        var result = await schedulerService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
