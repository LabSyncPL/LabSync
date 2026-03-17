using LabSync.Modules.SSH.Interfaces;
using LabSync.Modules.SSH.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabSync.Modules.SSH.Extensions;

/// <summary>
/// Extension methods for registering SSH module services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SSH module and its required services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSshModule(this IServiceCollection services)
    {
        services.AddSingleton<IFileTransferService, FileTransferService>();
        services.AddSingleton<ITunnelingService, TunnelingService>();
        services.AddSingleton<IRemoteShellService, RemoteShellService>();
        services.AddTransient<SshModule>();
        return services;
    }
}
