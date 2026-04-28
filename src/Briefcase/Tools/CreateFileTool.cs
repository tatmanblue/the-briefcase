using System.ComponentModel;
using System.Text.Json;
using Briefcase.Configuration;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class CreateFileTool
{
    private readonly AppSettings settings;
    private readonly FileRegistry registry;
    private readonly ProjectRegistry projectRegistry;

    public CreateFileTool(AppSettings settings, FileRegistry registry, ProjectRegistry projectRegistry)
    {
        this.settings = settings;
        this.registry = registry;
        this.projectRegistry = projectRegistry;
    }

    [McpServerTool(Name = "create_file")]
    [Description(
        "Creates a new file in the Briefcase and makes it immediately available to all agents. " +
        "Returns the new file's ID, which can be passed to read_file or update_file. " +
        "Optionally associates the file with a project by providing a projectId.")]
    public string CreateFile(
        [Description("The file name, including extension (e.g. 'notes.txt'). Path separators are stripped.")] string name,
        [Description("The full content of the new file.")] string content,
        [Description("Optional project ID (GUID) to associate the new file with. The call fails if the ID does not exist.")] Guid? projectId = null)
    {
        if (string.IsNullOrEmpty(settings.NewPath))
            return JsonSerializer.Serialize(new { error = "New-files directory is not configured (BRIEFCASE_NEW_PATH)." });

        if (projectId.HasValue && projectRegistry.GetById(projectId.Value) == null)
            return JsonSerializer.Serialize(new { error = $"Project '{projectId.Value}' not found." });

        var safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            return JsonSerializer.Serialize(new { error = "Invalid file name." });

        var absolutePath = Path.Combine(settings.NewPath, safeName);

        if (File.Exists(absolutePath))
            return JsonSerializer.Serialize(new { error = $"A file named '{safeName}' already exists. Use update_file to overwrite it." });

        try
        {
            File.WriteAllText(absolutePath, content);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to write file '{safeName}': {ex.Message}" });
        }

        var fileId = registry.AddOrUpdate(absolutePath);
        var info = new FileInfo(absolutePath);

        if (projectId.HasValue && fileId.HasValue)
            projectRegistry.AddFile(projectId.Value, fileId.Value);

        return JsonSerializer.Serialize(
            new
            {
                id = fileId,
                name = safeName,
                size = info.Length,
                lastModified = info.LastWriteTimeUtc,
                projectId
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
