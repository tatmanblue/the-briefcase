using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ListProjectsTool
{
    private readonly ProjectRegistry projectRegistry;

    public ListProjectsTool(ProjectRegistry projectRegistry)
    {
        this.projectRegistry = projectRegistry;
    }

    [McpServerTool(Name = "list_projects")]
    [Description("Lists all projects in the Briefcase. Returns project IDs, names, descriptions, and file counts.")]
    public string ListProjects(
        [Description("Maximum number of projects to return. Omit to return all.")] int? limit = null,
        [Description("Sort order. 'name_asc' = A to Z (default), 'name_desc' = Z to A.")] string? sort = null)
    {
        var projects = projectRegistry.GetAll();

        var sortKey = (sort ?? "name_asc").Trim().ToLowerInvariant();
        IEnumerable<ProjectEntry> sorted = sortKey == "name_desc"
            ? projects.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase)
            : projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        if (limit.HasValue && limit.Value > 0)
            sorted = sorted.Take(limit.Value);

        return JsonSerializer.Serialize(
            sorted.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                description = p.Description,
                createdDate = p.CreatedDate,
                fileCount = p.FileIds.Count
            }),
            new JsonSerializerOptions { WriteIndented = true });
    }
}
