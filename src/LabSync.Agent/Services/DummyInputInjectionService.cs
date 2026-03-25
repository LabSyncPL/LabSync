using Microsoft.Extensions.Logging;
using LabSync.Core.Interfaces;

namespace LabSync.Agent.Services;

/// <summary>
/// Placeholder implementation of IInputInjectionService for non-Windows platforms.
/// It does nothing but log the actions, preventing DI crashes on startup.
/// </summary>
public sealed class DummyInputInjectionService : IInputInjectionService
{
    private readonly ILogger<DummyInputInjectionService> _logger;

    public DummyInputInjectionService(ILogger<DummyInputInjectionService> logger)
    {
        _logger = logger;
    }

    public void MoveMouse(double normalizedX, double normalizedY)
    {
        _logger.LogDebug("Placeholder: Moved mouse to ({X}, {Y})", normalizedX, normalizedY);
    }

    public void SendMouseButton(MouseButton button, bool down)
    {
        _logger.LogDebug("Placeholder: Mouse button {Button} state down={Down}", button, down);
    }

    public void ScrollWheel(int delta)
    {
        _logger.LogDebug("Placeholder: Scrolled wheel by {Delta}", delta);
    }

    public void SendKey(ushort virtualKeyCode, bool down)
    {
        _logger.LogDebug("Placeholder: Key {Key} state down={Down}", virtualKeyCode, down);
    }

    public void SendUnicodeChar(char character, bool down)
    {
        _logger.LogDebug("Placeholder: Unicode char {Char} state down={Down}", character, down);
    }
}