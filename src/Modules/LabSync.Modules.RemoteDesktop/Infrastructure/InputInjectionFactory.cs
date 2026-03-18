using LabSync.Modules.RemoteDesktop.Abstractions;
using LabSync.Modules.RemoteDesktop.Input;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace LabSync.Modules.RemoteDesktop.Infrastructure;

public static class InputInjectionFactory
{
    public static IInputInjectionFactory Create(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory
            ? factory.CreateLogger<PlaceholderInputInjectionService>()
            : null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsInputInjectionFactory(logger);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxInputInjectionFactory(logger);

        return new PlaceholderInputInjectionFactory(logger);
    }
}

internal sealed class WindowsInputInjectionFactory : IInputInjectionFactory
{
    private readonly ILogger? _logger;

    public WindowsInputInjectionFactory(ILogger? logger) => _logger = logger;

    public IInputInjectionService Create() => new WindowsInputInjectionService(_logger);
}

internal sealed class LinuxInputInjectionFactory : IInputInjectionFactory
{
    private readonly ILogger? _logger;

    public LinuxInputInjectionFactory(ILogger? logger) => _logger = logger;

    public IInputInjectionService Create() => new PlaceholderInputInjectionService(_logger);
}

internal sealed class PlaceholderInputInjectionFactory : IInputInjectionFactory
{
    private readonly ILogger? _logger;

    public PlaceholderInputInjectionFactory(ILogger? logger) => _logger = logger;

    public IInputInjectionService Create() => new PlaceholderInputInjectionService(_logger);
}

internal sealed class PlaceholderInputInjectionService : IInputInjectionService
{
    private readonly ILogger? _logger;

    public PlaceholderInputInjectionService(ILogger? logger) => _logger = logger;

    public Task InjectMouseMoveAsync(int x, int y, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InjectMouseButtonAsync(MouseButton button, bool pressed, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InjectMouseWheelAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InjectKeyAsync(int keyCode, bool pressed, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
