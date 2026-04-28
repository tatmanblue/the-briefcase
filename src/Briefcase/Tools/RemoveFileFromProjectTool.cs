using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class RemoveFileFromProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public RemoveFileFromProjectTool(
        ProjectRegistry projectRegistry,
        NotificationDispatcher notificationDispatcher)
    {
        this.projectRegistry = projectRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "remove_file_from_project")]
    [Description(
        "Removes a file from a project. The file remains in the Briefcase but loses its project association.")]
    public async Task<string> RemoveFileFromProject(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("The file ID (GUID).")] Guid fileId)
    {
        if (projectRegistry.GetById(projectId) == null)
            return JsonSerializer.Serialize(new { error = "Project not found." });

        if (!projectRegistry.RemoveFile(projectId, fileId))
            return JsonSerializer.Serialize(new { error = "File is not a member of this project." });

        await notificationDispatcher.SendProjectListChangedAsync();

        return JsonSerializer.Serialize(
            new { success = true, projectId, fileId },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
