using LabSync.Core.Interfaces;

namespace LabSync.Agent.Services;

public sealed class InputInjectionHubHandler
{
    private readonly IAgentHubInvoker _hub;
    private readonly IInputInjectionService _input;

    public InputInjectionHubHandler(
        IAgentHubInvoker hub,
        IInputInjectionService input)
    {
        _hub = hub;
        _input = input;
    }

    public void Register()
    {
        _hub.RegisterHandler<double, double>("MouseMove",
            (nx, ny) => _input.MoveMouse(nx, ny));

        _hub.RegisterHandler<int, bool>("MouseButton",
            (btn, down) => _input.SendMouseButton((MouseButton)btn, down));

        _hub.RegisterHandler<int>("MouseWheel",
            delta => _input.ScrollWheel(delta));

        _hub.RegisterHandler<ushort, bool>("KeyEvent",
            (vk, down) => _input.SendKey(vk, down));

        _hub.RegisterHandler<char, bool>("CharEvent",
            (ch, down) => _input.SendUnicodeChar(ch, down));
    }
}