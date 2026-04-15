namespace LabSync.Core.Dto;

public sealed class DeviceGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DeviceCount { get; set; }
    public DeviceGroupDeviceDto[] Devices { get; set; } = [];
}

public sealed class DeviceGroupDeviceDto
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = "";
    public bool IsOnline { get; set; }
}

public sealed class CreateDeviceGroupRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class UpdateDeviceGroupRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class AssignDeviceGroupRequest
{
    public Guid GroupId { get; set; }
}
