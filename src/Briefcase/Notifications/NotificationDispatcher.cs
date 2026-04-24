using Briefcase.Registry;
using Briefcase.Watching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Briefcase.Notifications;

/// <summary>
/// Hosted service that bridges FileWatcher change events to MCP resource notifications.
/// Sends notifications/resources/list_changed when files are added, deleted, or renamed.
/// Sends notifications/resources/updated when a tracked file's content changes.
/// </summary>
public class NotificationDispatcher : IHostedService
{
    private readonly FileWatcher watcher;
    private readonly McpServer server;
    private readonly ILogger<NotificationDispatcher> logger;

    public NotificationDispatcher(FileWatcher watcher, McpServer server, ILogger<NotificationDispatcher> logger)
    {
        this.watcher = watcher;
        this.server = server;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        watcher.FileChanged += OnFileChanged;
        logger.LogInformation("NotificationDispatcher started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        watcher.FileChanged -= OnFileChanged;
        return Task.CompletedTask;
    }

    private async void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        try
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Renamed:
                    logger.LogDebug("Sending resource list changed notification ({ChangeType}): {Path}", e.ChangeType, e.AbsolutePath);
                    await server.SendNotificationAsync(NotificationMethods.ResourceListChangedNotification);
                    break;

                case WatcherChangeTypes.Changed:
                    logger.LogDebug("Sending resource updated notification: {Path}", e.AbsolutePath);
                    await server.SendNotificationAsync(
                        NotificationMethods.ResourceUpdatedNotification,
                        new ResourceUpdatedNotificationParams { Uri = BuildResourceUri(e.AbsolutePath) },
                        ModelContextProtocol.McpJsonUtilities.DefaultOptions);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send MCP notification for change: {Path}", e.AbsolutePath);
        }
    }

    private static string BuildResourceUri(string absolutePath) =>
        $"briefcase://file/{Uri.EscapeDataString(absolutePath)}";
}
