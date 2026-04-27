using System.ComponentModel;
using System.Text.Json;
using Briefcase.Configuration;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ListFilesTool
{
    private readonly FileRegistry registry;
    private readonly AppSettings appSettings;

    public ListFilesTool(FileRegistry registry, AppSettings appSettings)
    {
        this.registry = registry;
        this.appSettings = appSettings;
    }

    [McpServerTool(Name = "list_files")]
    [Description(
        "Lists files available in the Briefcase. Returns file IDs, names, sizes, and last modified timestamps. " +
        "Use the returned ID to read a file's content. " +
        "Results are sorted newest-modified first by default. " +
        "Use 'limit' to control how many results are returned and 'sort' to control ordering.")]
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
            "'default' = registry insertion order.")] string? sort = null)
    {
        var entries = registry.GetAll()
            .Select(entry =>
            {
                var info = new FileInfo(entry.AbsolutePath);
                return new
                {
                    entry,
                    id = entry.Id,
                    name = entry.Name,
                    size = info.Exists ? info.Length : (long?)null,
                    lastModified = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null
                };
            })
            .ToList();

        var sortKey = (sort ?? "modified_desc").Trim().ToLowerInvariant();
        IEnumerable<dynamic> sorted = sortKey switch
        {
            "modified_asc" => entries.OrderBy(f => f.lastModified ?? DateTime.MinValue),
            "name_asc"     => entries.OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase),
            "name_desc"    => entries.OrderByDescending(f => f.name, StringComparer.OrdinalIgnoreCase),
            "default"      => entries,
            _              => entries.OrderByDescending(f => f.lastModified ?? DateTime.MinValue) // modified_desc
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
                lastModified = f.lastModified
            })
            .ToList();

        return JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
    }
}
