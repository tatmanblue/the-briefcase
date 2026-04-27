using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class UpdateFileTool
{
    private readonly FileRegistry registry;

    public UpdateFileTool(FileRegistry registry)
    {
        this.registry = registry;
    }

    [McpServerTool(Name = "update_file")]
    [Description("Replaces the full content of an existing Briefcase file. The file is identified by the ID returned from list_files or create_file.")]
    public string UpdateFile(
        [Description("The file ID (GUID) returned by list_files or create_file.")] Guid id,
        [Description("The new full content of the file. The entire existing content is replaced.")] string content)
    {
        var entry = registry.GetById(id);
        if (entry is null)
            return JsonSerializer.Serialize(new { error = $"No file found with ID '{id}'." });

        if (!File.Exists(entry.AbsolutePath))
            return JsonSerializer.Serialize(new { error = $"File '{entry.Name}' is registered but no longer exists on disk." });

        try
        {
            File.WriteAllText(entry.AbsolutePath, content);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to write file '{entry.Name}': {ex.Message}" });
        }

        var info = new FileInfo(entry.AbsolutePath);

        return JsonSerializer.Serialize(
            new
            {
                id = entry.Id,
                name = entry.Name,
                size = info.Length,
                lastModified = info.LastWriteTimeUtc
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
