using System.Text.Json;
using System.Text.RegularExpressions;
using Briefcase.Configuration;
using Briefcase.Registry;
using Briefcase.Watching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Briefcase.Search;

public class SearchCache : IHostedService
{
    private readonly string cacheFilePath;
    private readonly bool enabled;
    private readonly int searchMaxFileSizeKb;
    private readonly FileWatcher watcher;
    private readonly Dictionary<string, SearchCacheEntry> entriesByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object lockObject = new();
    private readonly ILogger<SearchCache> logger;

    private static readonly HashSet<string> IndexableExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".txt" };

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public SearchCache(AppSettings settings, FileWatcher watcher, ILogger<SearchCache> logger)
    {
        this.watcher = watcher;
        this.logger = logger;
        enabled = settings.SearchCacheEnabled;
        searchMaxFileSizeKb = settings.SearchMaxFileSizeKb;
        cacheFilePath = Path.Combine(settings.DataPath, "search-cache.json");

        if (!enabled)
            return;

        Load();
        logger.LogInformation("Search cache loaded with {Count} entries.", entriesByPath.Count);
    }

    public bool IsEnabled => enabled;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (enabled)
            watcher.FileChanged += OnFileChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (enabled)
            watcher.FileChanged -= OnFileChanged;
        return Task.CompletedTask;
    }

    public bool TryGetWords(string absolutePath, DateTime lastModifiedUtc, out HashSet<string>? words)
    {
        lock (lockObject)
        {
            if (entriesByPath.TryGetValue(absolutePath, out var entry) &&
                entry.LastModifiedUtc == lastModifiedUtc)
            {
                words = new HashSet<string>(entry.Words, StringComparer.OrdinalIgnoreCase);
                return true;
            }
        }
        words = null;
        return false;
    }

    public void UpdateEntry(string absolutePath, Guid fileId, DateTime lastModifiedUtc, HashSet<string> words)
    {
        lock (lockObject)
        {
            entriesByPath[absolutePath] = new SearchCacheEntry
            {
                FileId = fileId,
                LastModifiedUtc = lastModifiedUtc,
                Words = [.. words]
            };
            Save();
        }
    }

    public int Rebuild(IReadOnlyList<RegistryEntry> entries)
    {
        if (!enabled)
            return 0;

        long maxBytes = searchMaxFileSizeKb > 0 ? searchMaxFileSizeKb * 1024L : long.MaxValue;
        var rebuilt = new Dictionary<string, SearchCacheEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!IndexableExtensions.Contains(Path.GetExtension(entry.AbsolutePath)))
                continue;

            var info = new FileInfo(entry.AbsolutePath);
            if (!info.Exists || info.Length > maxBytes)
                continue;

            string text;
            try { text = File.ReadAllText(entry.AbsolutePath); }
            catch { continue; }

            rebuilt[entry.AbsolutePath] = new SearchCacheEntry
            {
                FileId = entry.Id,
                LastModifiedUtc = info.LastWriteTimeUtc,
                Words = [.. ExtractWords(text)]
            };
        }

        lock (lockObject)
        {
            entriesByPath.Clear();
            foreach (var (path, cacheEntry) in rebuilt)
                entriesByPath[path] = cacheEntry;
            Save();
        }

        logger.LogInformation("Search cache rebuilt with {Count} entries.", rebuilt.Count);
        return rebuilt.Count;
    }

    public static HashSet<string> ExtractWords(string text) =>
        new(Regex.Split(text.ToLowerInvariant(), @"[^\w]+").Where(w => w.Length > 0),
            StringComparer.Ordinal);

    private void OnFileChanged(object? sender, FileChangedEventArgs e)
    {
        lock (lockObject)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:
                    if (entriesByPath.Remove(e.AbsolutePath))
                    {
                        Save();
                        logger.LogDebug("Search cache invalidated: {Path}", e.AbsolutePath);
                    }
                    break;

                case WatcherChangeTypes.Deleted:
                    if (entriesByPath.Remove(e.AbsolutePath))
                    {
                        Save();
                        logger.LogDebug("Search cache entry removed: {Path}", e.AbsolutePath);
                    }
                    break;

                case WatcherChangeTypes.Renamed:
                    if (e.OldPath is not null && entriesByPath.TryGetValue(e.OldPath, out var existing))
                    {
                        entriesByPath.Remove(e.OldPath);
                        entriesByPath[e.AbsolutePath] = existing;
                        Save();
                        logger.LogDebug("Search cache entry renamed: {OldPath} -> {NewPath}",
                            e.OldPath, e.AbsolutePath);
                    }
                    break;
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(cacheFilePath))
            return;
        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, SearchCacheEntry>>(json) ?? [];
            foreach (var (path, entry) in dict)
                entriesByPath[path] = entry;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load search cache from {Path}. Starting empty.", cacheFilePath);
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new Dictionary<string, SearchCacheEntry>(entriesByPath), SerializerOptions);
            File.WriteAllText(cacheFilePath, json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save search cache to {Path}.", cacheFilePath);
        }
    }
}
