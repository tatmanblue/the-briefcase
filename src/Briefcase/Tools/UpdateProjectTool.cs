using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class UpdateProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public UpdateProjectTool(
        ProjectRegistry projectRegistry,
        NotificationDispatcher notificationDispatcher)
    {
        this.projectRegistry = projectRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "update_project")]
    [Description("Updates a project's name and/or description. Omit a parameter to leave it unchanged.")]
    public async Task<string> UpdateProject(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("New project name. Omit to keep current name.")] string? name = null,
        [Description("New project description. Omit to keep current description.")] string? description = null)
    {
        if (name == null && description == null)
            return JsonSerializer.Serialize(new { error = "Provide at least one of name or description to update." });

        var error = projectRegistry.Update(projectId, name?.Trim(), description);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        var entry = projectRegistry.GetById(projectId)!;
        await notificationDispatcher.SendProjectListChangedAsync();

        return JsonSerializer.Serialize(
            new
            {
                id = entry.Id,
                name = entry.Name,
                description = entry.Description,
                createdDate = entry.CreatedDate
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
