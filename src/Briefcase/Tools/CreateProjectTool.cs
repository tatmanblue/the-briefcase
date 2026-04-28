using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class CreateProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public CreateProjectTool(ProjectRegistry projectRegistry, NotificationDispatcher notificationDispatcher)
    {
        this.projectRegistry = projectRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "create_project")]
    [Description("Creates a new project in the Briefcase. Project names must be unique. Returns the new project's ID.")]
    public async Task<string> CreateProject(
        [Description("The project name. Must be unique.")] string name,
        [Description("A short description of the project.")] string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return JsonSerializer.Serialize(new { error = "name must not be empty." });

        var (entry, error) = projectRegistry.Create(name.Trim(), description ?? string.Empty);
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        await notificationDispatcher.SendProjectListChangedAsync();

        return JsonSerializer.Serialize(
            new
            {
                id = entry!.Id,
                name = entry.Name,
                description = entry.Description,
                createdDate = entry.CreatedDate
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
