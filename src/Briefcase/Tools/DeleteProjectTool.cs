using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class DeleteProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public DeleteProjectTool(
        ProjectRegistry projectRegistry,
        NotificationDispatcher notificationDispatcher)
    {
        this.projectRegistry = projectRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "delete_project")]
    [Description(
        "Deletes a project. All member files remain in the Briefcase but lose their project association.")]
    public async Task<string> DeleteProject(
        [Description("The project ID (GUID).")] Guid projectId)
    {
        if (!projectRegistry.Delete(projectId))
            return JsonSerializer.Serialize(new { error = "Project not found." });

        await notificationDispatcher.SendProjectListChangedAsync();

        return JsonSerializer.Serialize(
            new { success = true, projectId },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
