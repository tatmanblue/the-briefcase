using Briefcase.Configuration;
using Briefcase.Registry;
using Briefcase.Services.Content;

namespace Briefcase.Services;

public record FileListItem(
    Guid Id,
    string Name,
    long? Size,
    DateTime? LastModified,
    Guid? ProjectId,
    string? ProjectName,
    bool IsArchived,
    FileTypeInfo? FileType = null);

public record FileQueryOptions(
    int? Limit = null,
    string? Sort = null,
    string? Project = null,
    bool? Unassigned = null,
    bool? IncludeArchived = null,
    bool? ArchivedOnly = null);

public class FileQueryService
{
    private readonly FileRegistry registry;
    private readonly ProjectRegistry projectRegistry;
    private readonly AppSettings appSettings;
    private readonly FileTypeClassifier classifier;

    public FileQueryService(FileRegistry registry, ProjectRegistry projectRegistry, AppSettings appSettings, FileTypeClassifier classifier)
    {
        this.registry = registry;
        this.projectRegistry = projectRegistry;
        this.appSettings = appSettings;
        this.classifier = classifier;
    }

    public (IReadOnlyList<FileListItem>? Files, string? Error) GetFiles(FileQueryOptions options)
    {
        if (options.Project != null && options.Unassigned == true)
            return (null, "Cannot specify both 'project' and 'unassigned'.");

        if (options.ArchivedOnly == true && options.IncludeArchived == false)
            return (null, "Cannot specify 'archivedOnly' with 'includeArchived' set to false.");

        Guid? filterProjectId = null;
        if (options.Project != null)
        {
            ProjectEntry? projectEntry = Guid.TryParse(options.Project, out var parsedGuid)
                ? projectRegistry.GetById(parsedGuid)
                : projectRegistry.GetByName(options.Project);

            if (projectEntry == null)
                return (null, $"Project '{options.Project}' not found.");

            filterProjectId = projectEntry.Id;
        }

        var entries = registry.GetAll()
            .Select(entry =>
            {
                var info = new FileInfo(entry.AbsolutePath);
                var proj = projectRegistry.FindProjectForFile(entry.Id);
                return new FileListItem(
                    entry.Id,
                    entry.Name,
                    info.Exists ? info.Length : null,
                    info.Exists ? info.LastWriteTimeUtc : null,
                    proj?.projectId,
                    proj?.projectName,
                    entry.IsArchived);
            })
            .ToList();

        var afterArchiveFilter = options.ArchivedOnly == true
            ? entries.Where(f => f.IsArchived).ToList()
            : options.IncludeArchived != true
                ? entries.Where(f => !f.IsArchived).ToList()
                : entries;

        IEnumerable<FileListItem> filtered = afterArchiveFilter;
        if (filterProjectId.HasValue)
            filtered = afterArchiveFilter.Where(f => f.ProjectId == filterProjectId);
        else if (options.Unassigned == true)
            filtered = afterArchiveFilter.Where(f => f.ProjectId == null);

        var sortKey = (options.Sort ?? "modified_desc").Trim().ToLowerInvariant();
        IEnumerable<FileListItem> sorted = sortKey switch
        {
            "modified_asc" => filtered.OrderBy(f => f.LastModified ?? DateTime.MinValue),
            "name_asc"     => filtered.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            "name_desc"    => filtered.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
            "default"      => filtered,
            _              => filtered.OrderByDescending(f => f.LastModified ?? DateTime.MinValue)
        };

        var effectiveLimit = options.Limit ?? appSettings.ListFilesDefaultLimit;
        if (effectiveLimit > 0)
            sorted = sorted.Take(effectiveLimit);

        // Classified after limiting so unknown-extension files are only sniffed when actually returned.
        return (sorted.Select(f => f with { FileType = Classify(f) }).ToList(), null);
    }

    private FileTypeInfo Classify(FileListItem file)
    {
        var absolutePath = file.Size.HasValue ? registry.GetById(file.Id)?.AbsolutePath : null;
        return classifier.Classify(file.Name, absolutePath);
    }
}
