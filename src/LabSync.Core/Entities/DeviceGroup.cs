using System;
using System.Collections.Generic;

namespace LabSync.Core.Entities;

public class DeviceGroup
{
    public Guid Id { get; init; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; init; }

    private readonly List<Device> _devices = new();
    public IReadOnlyCollection<Device> Devices => _devices.AsReadOnly();

    protected DeviceGroup() { }

    public DeviceGroup(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdateDetails(name, description);
    }

    public void UpdateDetails(string name, string? description)
    {
        UpdateName(name);
        UpdateDescription(description);
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Group name cannot be empty.", nameof(name));

        Name = name;
    }

    public void AddDevice(Device device)
    {
        if (device is null)
            throw new ArgumentNullException(nameof(device));

        if (_devices.Contains(device))
            return;

        device.AssignToGroup(Id);
        _devices.Add(device);
    }

    public void RemoveDevice(Device device)
    {
        if (device is null)
            throw new ArgumentNullException(nameof(device));

        device.RemoveFromGroup();
        _devices.Remove(device);
    }
}