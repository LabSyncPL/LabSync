using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/saved-scripts")]
[Authorize(Policy = "RequireAdminRole")]
public sealed class SavedScriptsController(LabSyncDbContext dbContext) : ControllerBase
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 1_000;
    private const int MaxContentChars = 200_000;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavedScriptDto>>> GetAll(CancellationToken cancellationToken)
    {
        var scripts = await dbContext.SavedScripts
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => ToDto(s))
            .ToListAsync(cancellationToken);

        return Ok(scripts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavedScriptDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var script = await dbContext.SavedScripts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (script is null)
            return NotFound(new ApiResponse("Saved script not found."));

        return Ok(ToDto(script));
    }

    [HttpPost]
    public async Task<ActionResult<SavedScriptDto>> Create(
        [FromBody] CreateSavedScriptRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.Title, request.Description, request.Content, request.Interpreter);
        if (validation is not null)
            return BadRequest(new ApiResponse(validation));

        var entity = new SavedScript(
            request.Title.Trim(),
            request.Description,
            request.Content,
            request.Interpreter.ToLowerInvariant());

        dbContext.SavedScripts.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SavedScriptDto>> Update(
        Guid id,
        [FromBody] UpdateSavedScriptRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.Title, request.Description, request.Content, request.Interpreter);
        if (validation is not null)
            return BadRequest(new ApiResponse(validation));

        var entity = await dbContext.SavedScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(new ApiResponse("Saved script not found."));

        entity.Update(
            request.Title.Trim(),
            request.Description,
            request.Content,
            request.Interpreter.ToLowerInvariant());

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SavedScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
            return NotFound(new ApiResponse("Saved script not found."));

        dbContext.SavedScripts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(string title, string? description, string content, string interpreter)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Title is required.";
        if (title.Trim().Length > MaxTitleLength)
            return $"Title cannot exceed {MaxTitleLength} characters.";
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
            return $"Description cannot exceed {MaxDescriptionLength} characters.";
        if (string.IsNullOrWhiteSpace(content))
            return "Script content is required.";
        if (content.Length > MaxContentChars)
            return $"Script content is too large (max {MaxContentChars:N0} characters).";

        var normalized = interpreter.Trim().ToLowerInvariant();
        if (normalized is not ("bash" or "powershell" or "cmd"))
            return "Interpreter must be one of: bash, powershell, cmd.";

        return null;
    }

    private static SavedScriptDto ToDto(SavedScript savedScript)
    {
        return new SavedScriptDto
        {
            Id = savedScript.Id,
            Title = savedScript.Title,
            Description = savedScript.Description,
            Content = savedScript.Content,
            Interpreter = savedScript.Interpreter,
            CreatedAt = savedScript.CreatedAt,
            UpdatedAt = savedScript.UpdatedAt,
        };
    }
}
