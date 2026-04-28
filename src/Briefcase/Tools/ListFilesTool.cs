using System.ComponentModel;
using System.Text.Json;
using Briefcase.Configuration;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ListFilesTool
{
    private readonly FileRegistry registry;
    private readonly ProjectRegistry projectRegistry;
    private readonly AppSettings appSettings;

    public ListFilesTool(FileRegistry registry, ProjectRegistry projectRegistry, AppSettings appSettings)
    {
        this.registry = registry;
        this.projectRegistry = projectRegistry;
        this.appSettings = appSettings;
    }

    [McpServerTool(Name = "list_files")]
    [Description(
        "Lists files available in the Briefcase. Returns file IDs, names, sizes, last modified timestamps, and project association. " +
        "Use the returned ID to read a file's content. " +
        "Results are sorted newest-modified first by default. " +
        "Use 'limit' to control how many results are returned and 'sort' to control ordering. " +
        "Use 'project' to filter by project ID or name, or 'unassigned' to return only files not in any project.")]
    public string ListFiles(
        [Description(
            "Maximum number of files to return. " +
            "Omit to use the server-configured default. " +
            "Pass a positive integer to cap results. " +
            "Pass 0 or a negative value to return all files.")] int? limit = null,
        [Description(
            "Sort order for results. " +
            "'modified_desc' = newest modified first (default), " +
            "'modified_asc' = oldest modified first, " +
            "'name_asc' = alphabetical A to Z, " +
            "'name_desc' = alphabetical Z to A, " +
            "'default' = registry insertion order.")] string? sort = null,
        [Description("Filter by project ID (GUID) or project name. Cannot be combined with unassigned.")] string? project = null,
        [Description("When true, returns only files not associated with any project. Cannot be combined with project.")] bool? unassigned = null)
    {
        if (project != null && unassigned == true)
            return JsonSerializer.Serialize(new { error = "Cannot specify both 'project' and 'unassigned'." });

        Guid? filterProjectId = null;
        if (project != null)
        {
            ProjectEntry? projectEntry = Guid.TryParse(project, out var parsedGuid)
                ? projectRegistry.GetById(parsedGuid)
                : projectRegistry.GetByName(project);

            if (projectEntry == null)
                return JsonSerializer.Serialize(new { error = $"Project '{project}' not found." });

            filterProjectId = projectEntry.Id;
        }

        var entries = registry.GetAll()
            .Select(entry =>
            {
                var info = new FileInfo(entry.AbsolutePath);
                var proj = projectRegistry.FindProjectForFile(entry.Id);
                return new
                {
                    entry,
                    id = entry.Id,
                    name = entry.Name,
                    size = info.Exists ? info.Length : (long?)null,
                    lastModified = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null,
                    projectId = proj?.projectId,
                    projectName = proj?.projectName
                };
            })
            .ToList();

        // Apply project / unassigned filter
        IEnumerable<dynamic> filtered = entries;
        if (filterProjectId.HasValue)
            filtered = entries.Where(f => f.projectId == filterProjectId);
        else if (unassigned == true)
            filtered = entries.Where(f => f.projectId == null);

        var sortKey = (sort ?? "modified_desc").Trim().ToLowerInvariant();
        IEnumerable<dynamic> sorted = sortKey switch
        {
            "modified_asc" => filtered.OrderBy(f => f.lastModified ?? DateTime.MinValue),
            "name_asc"     => filtered.OrderBy(f => (string)f.name, StringComparer.OrdinalIgnoreCase),
            "name_desc"    => filtered.OrderByDescending(f => (string)f.name, StringComparer.OrdinalIgnoreCase),
            "default"      => filtered,
            _              => filtered.OrderByDescending(f => f.lastModified ?? DateTime.MinValue)
        };

        int effectiveLimit = limit.HasValue ? limit.Value : appSettings.ListFilesDefaultLimit;
        if (effectiveLimit > 0)
            sorted = sorted.Take(effectiveLimit);

        var files = sorted
            .Select(f => new
            {
                id = f.id,
                name = f.name,
                size = f.size,
                lastModified = f.lastModified,
                projectId = f.projectId,
                projectName = f.projectName
            })
            .ToList();

        return JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
    }
}
