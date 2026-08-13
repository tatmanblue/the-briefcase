using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Briefcase.Notifications;

/// <summary>
/// Sends MCP resource-change notifications on the calling tool's own session. Called explicitly by
/// tools right after they mutate state (create/update/delete file or project). Under the HTTP
/// transport, McpServer is per-session and isn't resolvable from the general DI container -- the
/// SDK only binds it as a special parameter on an [McpServerTool] method itself -- so tools must
/// take it as a parameter and pass it through here rather than this class holding one via its own
/// constructor.
/// </summary>
public class NotificationDispatcher
{
    private readonly ILogger<NotificationDispatcher> logger;

    public NotificationDispatcher(ILogger<NotificationDispatcher> logger)
    {
        this.logger = logger;
    }

    public async Task SendListChangedAsync(McpServer server)
    {
        try
        {
            await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification);
            logger.LogDebug("Sent resource list changed notification from reindex.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send resource list changed notification from reindex.");
        }
    }

    public async Task SendProjectListChangedAsync(McpServer server)
    {
        try
        {
            await server.SendNotificationAsync("notifications/projects/list_changed");
            logger.LogDebug("Sent project list changed notification.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send project list changed notification.");
        }
    }
}
