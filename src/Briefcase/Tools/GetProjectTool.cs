using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class GetProjectTool
{
    private readonly ProjectRegistry projectRegistry;
    private readonly FileRegistry fileRegistry;

    public GetProjectTool(ProjectRegistry projectRegistry, FileRegistry fileRegistry)
    {
        this.projectRegistry = projectRegistry;
        this.fileRegistry = fileRegistry;
    }

    [McpServerTool(Name = "get_project")]
    [Description("Returns a project's metadata and the list of files it contains. Accepts a project ID (GUID) or project name.")]
    public string GetProject(
        [Description("The project ID (GUID) or project name.")] string idOrName)
    {
        ProjectEntry? entry = null;

        if (Guid.TryParse(idOrName, out var guid))
            entry = projectRegistry.GetById(guid);

        entry ??= projectRegistry.GetByName(idOrName);

        if (entry == null)
            return JsonSerializer.Serialize(new { error = $"Project '{idOrName}' not found." });

        var files = entry.FileIds
            .Select(fileId => fileRegistry.GetById(fileId))
            .Where(f => f != null)
            .Select(f => new { id = f!.Id, name = f.Name })
            .ToList();

        return JsonSerializer.Serialize(
            new
            {
                id = entry.Id,
                name = entry.Name,
                description = entry.Description,
                createdDate = entry.CreatedDate,
                files
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
