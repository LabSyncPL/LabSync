using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LabSync.Server.Hubs;

/// <summary>
/// Admin-facing hub for script execution telemetry. Clients subscribe to a task group to receive output.
/// </summary>
[Authorize(Policy = "RequireAdminRole")]
public sealed class ScriptHub : Hub
{
    public const string GlobalGroupName = "all-scripts";

    public static string TaskGroupName(Guid taskId) => $"script-task:{taskId:N}";

    public override async Task OnConnectedAsync()
    {
        // Automatically join global group to see all executions (including scheduled)
        await Groups.AddToGroupAsync(Context.ConnectionId, GlobalGroupName);
        await base.OnConnectedAsync();
    }

    public Task SubscribeToTask(string taskId)
    {
        if (!Guid.TryParse(taskId, out var id))
            return Task.CompletedTask;

        return Groups.AddToGroupAsync(Context.ConnectionId, TaskGroupName(id));
    }

    public Task UnsubscribeFromTask(string taskId)
    {
        if (!Guid.TryParse(taskId, out var id))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, TaskGroupName(id));
    }
}
