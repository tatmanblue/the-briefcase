using System.ComponentModel;
using System.Text.Json;
using Briefcase.Services;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ListFilesTool
{
    private readonly FileQueryService queryService;

    public ListFilesTool(FileQueryService queryService)
    {
        this.queryService = queryService;
    }

    [McpServerTool(Name = "list_files")]
    [Description(
        "Lists files available in the Briefcase. Returns file IDs, names, sizes, last modified timestamps, and project association. " +
        "Use the returned ID to read a file's content. " +
        "Results are sorted newest-modified first by default. " +
        "Use 'limit' to control how many results are returned and 'sort' to control ordering. " +
        "Use 'project' to filter by project ID or name, or 'unassigned' to return only files not in any project. " +
        "Archived files are excluded by default; use 'includeArchived' to include them or 'archivedOnly' to return only archived files.")]
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
        [Description("When true, returns only files not associated with any project. Cannot be combined with project.")] bool? unassigned = null,
        [Description("When true, includes archived files alongside active files in the results.")] bool? includeArchived = null,
        [Description("When true, returns only archived files. Cannot be combined with includeArchived=false.")] bool? archivedOnly = null)
    {
        var (files, error) = queryService.GetFiles(new FileQueryOptions(limit, sort, project, unassigned, includeArchived, archivedOnly));
        if (error != null)
            return JsonSerializer.Serialize(new { error });

        var result = files!.Select(f => new
        {
            id = f.Id,
            name = f.Name,
            size = f.Size,
            lastModified = f.LastModified,
            projectId = f.ProjectId,
            projectName = f.ProjectName,
            isArchived = f.IsArchived
        });

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
