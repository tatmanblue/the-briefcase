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

    public CreateFileTool(AppSettings settings, FileRegistry registry)
    {
        this.settings = settings;
        this.registry = registry;
    }

    [McpServerTool(Name = "create_file")]
    [Description("Creates a new file in the Briefcase and makes it immediately available to all agents. Returns the new file's ID, which can be passed to read_file or update_file.")]
    public string CreateFile(
        [Description("The file name, including extension (e.g. 'notes.txt'). Path separators are stripped.")] string name,
        [Description("The full content of the new file.")] string content)
    {
        if (string.IsNullOrEmpty(settings.NewFilesDataPath))
            return JsonSerializer.Serialize(new { error = "New-files directory is not configured (BRIEFCASE_NEW_FILES_DATA_PATH)." });

        var safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            return JsonSerializer.Serialize(new { error = "Invalid file name." });

        var absolutePath = Path.Combine(settings.NewFilesDataPath, safeName);

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

        var id = registry.AddOrUpdate(absolutePath);
        var info = new FileInfo(absolutePath);

        return JsonSerializer.Serialize(
            new
            {
                id,
                name = safeName,
                size = info.Length,
                lastModified = info.LastWriteTimeUtc
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
