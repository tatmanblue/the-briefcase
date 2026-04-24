using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ReadFileTool
{
    private readonly FileRegistry registry;

    public ReadFileTool(FileRegistry registry)
    {
        this.registry = registry;
    }

    [McpServerTool(Name = "read_file")]
    [Description("Reads the content of a file by its ID. Returns the file content along with metadata (name, size, last modified). Use list_files to discover available file IDs.")]
    public string ReadFile(
        [Description("The file ID (GUID) returned by list_files.")] Guid id)
    {
        var entry = registry.GetById(id);
        if (entry is null)
            return JsonSerializer.Serialize(new { error = $"No file found with ID '{id}'." });

        if (!File.Exists(entry.AbsolutePath))
            return JsonSerializer.Serialize(new { error = $"File '{entry.Name}' is registered but no longer exists on disk." });

        var info = new FileInfo(entry.AbsolutePath);
        string content;
        try
        {
            content = File.ReadAllText(entry.AbsolutePath);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to read file '{entry.Name}': {ex.Message}" });
        }

        return JsonSerializer.Serialize(
            new
            {
                id = entry.Id,
                name = entry.Name,
                size = info.Length,
                lastModified = info.LastWriteTimeUtc,
                content
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
