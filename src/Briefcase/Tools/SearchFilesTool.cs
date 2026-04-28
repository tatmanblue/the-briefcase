using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Briefcase.Configuration;
using Briefcase.Registry;
using Briefcase.Search;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class SearchFilesTool
{
    private static readonly HashSet<string> IndexableExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".txt" };

    private readonly FileRegistry registry;
    private readonly ProjectRegistry projectRegistry;
    private readonly AppSettings appSettings;
    private readonly SearchCache searchCache;

    public SearchFilesTool(FileRegistry registry, ProjectRegistry projectRegistry, AppSettings appSettings, SearchCache searchCache)
    {
        this.registry = registry;
        this.projectRegistry = projectRegistry;
        this.appSettings = appSettings;
        this.searchCache = searchCache;
    }

    [McpServerTool(Name = "search_files")]
    [Description(
        "Searches files in the Briefcase by name and/or content. " +
        "Returns file IDs, names, sizes, last modified timestamps, where the match was found, and project association. " +
        "Content search covers .md and .txt files only. " +
        "Use 'project' to restrict the search to a specific project, or 'unassigned' to search only files not in any project.")]
    public string SearchFiles(
        [Description("Text to search for. Case-insensitive.")] string query,
        [Description(
            "Where to search. " +
            "'name' — filename only; " +
            "'content' — file contents only (.md and .txt files); " +
            "'both' — name and content (default).")] string? searchIn = null,
        [Description(
            "Match mode. " +
            "'substring' — matches anywhere within a word, e.g. 'auth' matches 'authentication' (default); " +
            "'word' — whole word only.")] string? matchMode = null,
        [Description(
            "Maximum number of results to return. " +
            "Omit to use the server-configured default. " +
            "Pass 0 or a negative value to return all matches.")] int? limit = null,
        [Description(
            "Sort order for results. " +
            "'modified_desc' = newest first (default), " +
            "'modified_asc' = oldest first, " +
            "'name_asc' = A to Z, " +
            "'name_desc' = Z to A, " +
            "'default' = registry order.")] string? sort = null,
        [Description("Filter by project ID (GUID) or project name. Cannot be combined with unassigned.")] string? project = null,
        [Description("When true, searches only files not associated with any project. Cannot be combined with project.")] bool? unassigned = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { error = "query must not be empty." });

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

        var searchInKey = (searchIn ?? "both").Trim().ToLowerInvariant();
        var matchModeKey = (matchMode ?? "substring").Trim().ToLowerInvariant();

        bool checkName = searchInKey is "name" or "both";
        bool checkContent = searchInKey is "content" or "both";

        var results = new List<SearchResult>();

        foreach (var entry in registry.GetAll())
        {
            var proj = projectRegistry.FindProjectForFile(entry.Id);

            if (filterProjectId.HasValue && proj?.projectId != filterProjectId)
                continue;
            if (unassigned == true && proj != null)
                continue;

            var info = new FileInfo(entry.AbsolutePath);
            if (!info.Exists) continue;

            bool nameMatch = checkName && Matches(entry.Name, query, matchModeKey);
            bool contentMatch = checkContent
                && IsIndexable(entry.AbsolutePath)
                && IsWithinSizeLimit(info)
                && MatchesContent(entry, info, query, matchModeKey);

            if (!nameMatch && !contentMatch) continue;

            results.Add(new SearchResult
            {
                Id = entry.Id,
                Name = entry.Name,
                Size = info.Length,
                LastModified = info.LastWriteTimeUtc,
                MatchedIn = (nameMatch && contentMatch) ? "both" : (nameMatch ? "name" : "content"),
                ProjectId = proj?.projectId,
                ProjectName = proj?.projectName
            });
        }

        var sortKey = (sort ?? "modified_desc").Trim().ToLowerInvariant();
        IEnumerable<SearchResult> sorted = sortKey switch
        {
            "modified_asc" => results.OrderBy(r => r.LastModified),
            "name_asc"     => results.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase),
            "name_desc"    => results.OrderByDescending(r => r.Name, StringComparer.OrdinalIgnoreCase),
            "default"      => results,
            _              => results.OrderByDescending(r => r.LastModified)
        };

        int effectiveLimit = limit.HasValue ? limit.Value : appSettings.SearchDefaultLimit;
        if (effectiveLimit > 0)
            sorted = sorted.Take(effectiveLimit);

        return JsonSerializer.Serialize(
            sorted.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                size = r.Size,
                lastModified = r.LastModified,
                matchedIn = r.MatchedIn,
                projectId = r.ProjectId,
                projectName = r.ProjectName
            }),
            new JsonSerializerOptions { WriteIndented = true });
    }

    private bool MatchesContent(RegistryEntry entry, FileInfo info, string query, string matchModeKey)
    {
        if (searchCache.IsEnabled)
        {
            if (!searchCache.TryGetWords(entry.AbsolutePath, info.LastWriteTimeUtc, out var words))
            {
                string text;
                try { text = File.ReadAllText(entry.AbsolutePath); }
                catch { return false; }
                words = SearchCache.ExtractWords(text);
                searchCache.UpdateEntry(entry.AbsolutePath, entry.Id, info.LastWriteTimeUtc, words);
            }
            return matchModeKey == "word"
                ? words!.Contains(query.ToLowerInvariant())
                : words!.Any(w => w.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        string fileText;
        try { fileText = File.ReadAllText(entry.AbsolutePath); }
        catch { return false; }

        return matchModeKey == "word"
            ? Regex.IsMatch(fileText, $@"\b{Regex.Escape(query)}\b", RegexOptions.IgnoreCase)
            : fileText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(string text, string query, string matchModeKey) =>
        matchModeKey == "word"
            ? Regex.IsMatch(text, $@"\b{Regex.Escape(query)}\b", RegexOptions.IgnoreCase)
            : text.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool IsIndexable(string path) =>
        IndexableExtensions.Contains(Path.GetExtension(path));

    private bool IsWithinSizeLimit(FileInfo info) =>
        appSettings.SearchMaxFileSizeKb <= 0 || info.Length <= appSettings.SearchMaxFileSizeKb * 1024L;

    private sealed class SearchResult
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public long Size { get; init; }
        public DateTime LastModified { get; init; }
        public string MatchedIn { get; init; } = string.Empty;
        public Guid? ProjectId { get; init; }
        public string? ProjectName { get; init; }
    }
}
