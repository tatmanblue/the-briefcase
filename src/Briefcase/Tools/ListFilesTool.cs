using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ListFilesTool
{
    private readonly FileRegistry registry;

    public ListFilesTool(FileRegistry registry)
    {
        this.registry = registry;
    }

    [McpServerTool(Name = "list_files")]
    [Description("Lists all files available in the Briefcase. Returns file IDs, names, sizes, and last modified timestamps. Use the returned ID to read a file's content.")]
    public string ListFiles()
    {
        var files = registry.GetAll()
            .Select(entry =>
            {
                var info = new FileInfo(entry.AbsolutePath);
                return new
                {
                    id = entry.Id,
                    name = entry.Name,
                    size = info.Exists ? info.Length : (long?)null,
                    lastModified = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null
                };
            })
            .ToList();

        return JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
    }
}
