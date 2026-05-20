---
sidebar_position: 1
---

# Agent Module Development

LabSync supports custom agent functionality through a modular plugin system. This page explains how to create, build, and deploy a new agent module.

## Module Architecture

An agent module is a .NET class library that implements the `IAgentModule` interface.

### Core responsibilities

- initialize services
- register command handlers
- execute work requests
- return structured results

### Interface Contract

A typical module implements:

```csharp
public interface IAgentModule
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(IServiceProvider serviceProvider);
    bool CanHandle(string jobType);
    Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken);
}
```

## Creating a New Module

### Project Setup

1. Create a new Class Library in the `LabSync.Modules` folder.
2. Target `.NET 9`.
3. Add references to required LabSync packages.

### Example Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\LabSync.Core\LabSync.Core.csproj" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Example Module

```csharp
using LabSync.Core.Interfaces;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LabSync.Modules.Example
{
    public class ExampleModule : IAgentModule
    {
        public string Name => "ExampleModule";
        public string Version => "1.0.0";

        public Task InitializeAsync(IServiceProvider serviceProvider)
        {
            // Register services or perform initialization
            return Task.CompletedTask;
        }

        public bool CanHandle(string jobType)
        {
            return jobType == "ExampleCommand";
        }

        public Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            var message = parameters.ContainsKey("message") ? parameters["message"] : "Hello from Example Module";
            return Task.FromResult(new ModuleResult
            {
                Success = true,
                Output = message,
                ExitCode = 0
            });
        }
    }
}
```

## Module Deployment

### Build the Module

```bash
dotnet build LabSync.Modules.Example.csproj -c Release
```

### Deploy to Agent

1. Copy the compiled DLL to the agent `Modules` folder:
   - Windows: `C:\Program Files\LabSync.Agent\Modules`
   - Linux: `/opt/labsync-agent/Modules`
2. Restart the agent service.

### Verify Module Loading

- Check agent startup logs for module discovery messages.
- Open the dashboard and verify new capabilities.

## Dependency Injection

Modules can use dependency injection to obtain shared services.

### Example

```csharp
public class ExampleModule : IAgentModule
{
    private readonly ILogger<ExampleModule> _logger;

    public ExampleModule(ILogger<ExampleModule> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(IServiceProvider serviceProvider)
    {
        // Example of resolving a service
        var config = serviceProvider.GetService<IConfiguration>();
        _logger.LogInformation("Example module initialized.");
        return Task.CompletedTask;
    }
}
```

## Best Practices

- Keep module responsibilities small and focused.
- Validate all incoming parameters.
- Avoid long-running synchronous operations.
- Use the existing agent logging framework.
- Do not bypass the server authentication model.

## Common Extension Points

### Command Handling

Implement `CanHandle` for each job type.

### Telemetry Collection

Use the module to collect custom device data.

### Custom Job Results

Return detailed output and structured metadata for dashboard display.

## Testing

### Local Testing

1. Build and deploy to a development agent.
2. Start the agent service.
3. Use the dashboard or API to send a command handled by the module.

### Debugging

- Use log messages to trace module loading.
- Verify the module DLL is present in the `Modules` folder.
- Confirm the module assembly is compatible with .NET 9.

## Module Lifecycle

1. Agent starts.
2. Module loader discovers DLLs.
3. Each module is instantiated.
4. `InitializeAsync` is called.
5. Server requests are routed to modules via `CanHandle`.
6. `ExecuteAsync` runs the requested work.

## Supported Scenarios

- custom telemetry collection
- proprietary command execution
- integration with third-party services
- device configuration tasks
- specialized data export

---

Next: [System Architecture](../architecture/overview)
