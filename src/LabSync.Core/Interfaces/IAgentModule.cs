namespace LabSync.Core.Interfaces;

public interface IAgentModule
{
    string Name { get; }
    string Version { get; }

    Task InitializeAsync(IServiceProvider serviceProvider);
    bool CanHandle(string jobType);
    Task<ModuleResult> ExecuteAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken);
}

public record ModuleResult
{
    public bool IsSuccess { get; init; }
    public object? Data { get; init; }
    public string? ErrorMessage { get; init; }

    private ModuleResult() { }

    public static ModuleResult Success(object? data = null) =>
        new() { IsSuccess = true, Data = data };

    public static ModuleResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}