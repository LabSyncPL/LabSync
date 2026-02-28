namespace LabSync.Agent.Modules.RemoteDesktop.Abstractions;

public interface IInputInjectionService : IAsyncDisposable
{
    Task InjectMouseMoveAsync(int x, int y, CancellationToken cancellationToken = default);
    Task InjectMouseButtonAsync(MouseButton button, bool pressed, CancellationToken cancellationToken = default);
    Task InjectMouseWheelAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default);
    Task InjectKeyAsync(int keyCode, bool pressed, CancellationToken cancellationToken = default);
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}
