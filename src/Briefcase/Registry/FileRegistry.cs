using System.Text.Json;
using Briefcase.Configuration;
using Briefcase.Exclusions;
using Microsoft.Extensions.Logging;

namespace Briefcase.Registry;

public class FileRegistry
{
    private readonly string registryFilePath;
    private readonly Dictionary<Guid, RegistryEntry> entriesById = new();
    private readonly Dictionary<string, Guid> pathToId = new();
    private readonly object lockObject = new();
    private readonly IgnoreRules ignoreRules;
    private readonly string[] watchedRoots;
    private readonly ILogger<FileRegistry> logger;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public FileRegistry(AppSettings settings, IgnoreRules ignoreRules, ILogger<FileRegistry> logger)
    {
        this.ignoreRules = ignoreRules;
        this.logger = logger;
        watchedRoots = settings.BriefcasePaths;
        Directory.CreateDirectory(settings.DataPath);
        registryFilePath = Path.Combine(settings.DataPath, "registry.json");
        Load();
        Scan(settings.BriefcasePaths);
        Save();
        this.logger.LogInformation("Registry loaded with {Count} entries.", entriesById.Count);
    }

    public IReadOnlyList<RegistryEntry> GetAll()
    {
        lock (lockObject)
            return entriesById.Values.ToList();
    }

    public RegistryEntry? GetById(Guid id)
    {
        lock (lockObject)
            return entriesById.TryGetValue(id, out var entry) ? entry : null;
    }

    public Guid? AddOrUpdate(string absolutePath)
    {
        if (ignoreRules.IsExcluded(absolutePath, watchedRoots))
        {
            logger.LogDebug("Skipping excluded file: {Path}", absolutePath);
            return null;
        }

        lock (lockObject)
        {
            if (pathToId.TryGetValue(absolutePath, out var existingId))
                return existingId;

            var entry = new RegistryEntry { Id = Guid.NewGuid(), AbsolutePath = absolutePath };
            entriesById[entry.Id] = entry;
            pathToId[absolutePath] = entry.Id;
            Save();
            logger.LogDebug("Registered new file: {Path} -> {Id}", absolutePath, entry.Id);
            return entry.Id;
        }
    }

    public void Remove(string absolutePath)
    {
        lock (lockObject)
        {
            if (!pathToId.TryGetValue(absolutePath, out var id))
                return;

            entriesById.Remove(id);
            pathToId.Remove(absolutePath);
            Save();
            logger.LogDebug("Removed file from registry: {Path}", absolutePath);
        }
    }

    public void Rename(string oldPath, string newPath)
    {
        lock (lockObject)
        {
            if (!pathToId.TryGetValue(oldPath, out var id))
                return;

            pathToId.Remove(oldPath);

            if (ignoreRules.IsExcluded(newPath, watchedRoots))
            {
                // New name matches an exclusion — drop it from the registry entirely
                entriesById.Remove(id);
                logger.LogDebug("Renamed file matches exclusion, removed from registry: {Path}", newPath);
            }
            else
            {
                entriesById[id] = new RegistryEntry { Id = id, AbsolutePath = newPath };
                pathToId[newPath] = id;
                logger.LogDebug("Renamed file in registry: {OldPath} -> {NewPath}", oldPath, newPath);
            }

            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(registryFilePath))
            return;

        try
        {
            var json = File.ReadAllText(registryFilePath);
            var entries = JsonSerializer.Deserialize<List<RegistryEntry>>(json) ?? [];
            foreach (var entry in entries)
            {
                entriesById[entry.Id] = entry;
                pathToId[entry.AbsolutePath] = entry.Id;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load registry from {Path}. Starting with empty registry.", registryFilePath);
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(entriesById.Values.ToList(), SerializerOptions);
        File.WriteAllText(registryFilePath, json);
    }

    public (int added, int pruned, IReadOnlyList<Guid> prunedIds) Reindex()
    {
        lock (lockObject)
        {
            var (added, prunedIds) = Scan(watchedRoots);
            Save();
            logger.LogInformation("Reindex complete: {Added} added, {Pruned} pruned.", added, prunedIds.Count);
            return (added, prunedIds.Count, prunedIds);
        }
    }

    private (int added, IReadOnlyList<Guid> prunedIds) Scan(string[] paths)
    {
        int added = 0;

        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
            {
                logger.LogWarning("Configured path does not exist and will be skipped: {Path}", path);
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (pathToId.ContainsKey(file))
                    continue;

                if (ignoreRules.IsExcluded(file, watchedRoots))
                {
                    logger.LogDebug("Excluding file during scan: {Path}", file);
                    continue;
                }

                var entry = new RegistryEntry { Id = Guid.NewGuid(), AbsolutePath = file };
                entriesById[entry.Id] = entry;
                pathToId[file] = entry.Id;
                added++;
            }
        }

        // Prune entries for files that no longer exist on disk
        var stale = entriesById.Values
            .Where(e => !File.Exists(e.AbsolutePath) || ignoreRules.IsExcluded(e.AbsolutePath, watchedRoots))
            .Select(e => e.Id)
            .ToList();

        foreach (var id in stale)
        {
            var stalePath = entriesById[id].AbsolutePath;
            entriesById.Remove(id);
            pathToId.Remove(stalePath);
            logger.LogDebug("Pruned stale registry entry: {Path}", stalePath);
        }

        return (added, stale);
    }
}
