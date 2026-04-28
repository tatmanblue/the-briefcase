using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class AddFileToProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly FileRegistry fileRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public AddFileToProjectTool(
        ProjectRegistry projectRegistry,
        FileRegistry fileRegistry,
        NotificationDispatcher notificationDispatcher)
    {
        this.projectRegistry = projectRegistry;
        this.fileRegistry = fileRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "add_file_to_project")]
    [Description(
        "Associates a file with a project. A file can belong to at most one project. " +
        "If the file is already in another project it is moved to this one.")]
    public async Task<string> AddFileToProject(
        [Description("The project ID (GUID).")] Guid projectId,
        [Description("The file ID (GUID) returned by list_files.")] Guid fileId)
    {
        if (projectRegistry.GetById(projectId) == null)
            return JsonSerializer.Serialize(new { error = "Project not found." });

        if (fileRegistry.GetById(fileId) == null)
            return JsonSerializer.Serialize(new { error = "File not found." });

        projectRegistry.AddFile(projectId, fileId);
        await notificationDispatcher.SendProjectListChangedAsync();

        return JsonSerializer.Serialize(
            new { success = true, projectId, fileId },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
