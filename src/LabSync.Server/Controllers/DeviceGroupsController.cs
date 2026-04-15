using LabSync.Core.Dto;
using LabSync.Core.Entities;
using LabSync.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabSync.Server.Controllers;

[ApiController]
[Route("api/device-groups")]
[Authorize(Policy = "RequireAdminRole")]
public sealed class DeviceGroupsController(LabSyncDbContext context) : ControllerBase
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await context.Set<DeviceGroup>()
            .AsNoTracking()
            .Include(g => g.Devices)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        return Ok(groups.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<DeviceGroupDto>> Create(
        [FromBody] CreateDeviceGroupRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.Name, request.Description);
        if (validation is not null)
            return BadRequest(new ApiResponse(validation));

        var normalizedName = request.Name.Trim();
        var exists = await context.Set<DeviceGroup>()
            .AnyAsync(g => g.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
            return BadRequest(new ApiResponse("A group with this name already exists."));

        var group = new DeviceGroup(normalizedName, request.Description?.Trim());
        context.Set<DeviceGroup>().Add(group);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = group.Id }, ToDto(group));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeviceGroupDto>> Update(
        Guid id,
        [FromBody] UpdateDeviceGroupRequest request,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request.Name, request.Description);
        if (validation is not null)
            return BadRequest(new ApiResponse(validation));

        var group = await context.Set<DeviceGroup>()
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
            return NotFound(new ApiResponse("Group not found."));

        var normalizedName = request.Name.Trim();
        var duplicate = await context.Set<DeviceGroup>()
            .AnyAsync(g => g.Id != id && g.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (duplicate)
            return BadRequest(new ApiResponse("A group with this name already exists."));

        group.UpdateDetails(normalizedName, request.Description?.Trim());
        await context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(group));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var group = await context.Set<DeviceGroup>().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null)
            return NotFound(new ApiResponse("Group not found."));

        context.Set<DeviceGroup>().Remove(group);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Group name is required.";
        if (name.Trim().Length > MaxNameLength)
            return $"Group name cannot exceed {MaxNameLength} characters.";
        if (!string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength)
            return $"Description cannot exceed {MaxDescriptionLength} characters.";
        return null;
    }

    private static DeviceGroupDto ToDto(DeviceGroup group)
    {
        var devices = group.Devices.Select(d => new DeviceGroupDeviceDto
        {
            Id = d.Id,
            Hostname = d.Hostname,
            IsOnline = d.IsOnline
        }).ToArray();

        return new DeviceGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            CreatedAt = group.CreatedAt,
            DeviceCount = devices.Length,
            Devices = devices
        };
    }
}
