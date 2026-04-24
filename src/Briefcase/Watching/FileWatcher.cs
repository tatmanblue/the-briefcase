using Briefcase.Configuration;
using Briefcase.Exclusions;
using Briefcase.Registry;
using Microsoft.Extensions.Logging;

namespace Briefcase.Watching;

public class FileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly FileRegistry registry;
    private readonly IgnoreRules ignoreRules;
    private readonly string[] watchedRoots;
    private readonly ILogger<FileWatcher> logger;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public FileWatcher(AppSettings settings, FileRegistry registry, IgnoreRules ignoreRules, ILogger<FileWatcher> logger)
    {
        this.registry = registry;
        this.ignoreRules = ignoreRules;
        this.logger = logger;
        watchedRoots = settings.BriefcasePaths;

        foreach (var path in settings.BriefcasePaths)
        {
            if (!Directory.Exists(path))
            {
                this.logger.LogWarning("Skipping watch on non-existent path: {Path}", path);
                continue;
            }

            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watchers.Add(watcher);
            this.logger.LogInformation("Watching path: {Path}", path);
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        if (ignoreRules.IsExcluded(e.FullPath, watchedRoots))
        {
            logger.LogDebug("Ignoring created file (excluded): {Path}", e.FullPath);
            return;
        }
        registry.AddOrUpdate(e.FullPath);
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Created);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        if (ignoreRules.IsExcluded(e.FullPath, watchedRoots))
        {
            logger.LogDebug("Ignoring changed file (excluded): {Path}", e.FullPath);
            return;
        }
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Changed);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        // Always process deletes — the file may have been in the registry before exclusions were updated
        registry.Remove(e.FullPath);
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Deleted);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        registry.Rename(e.OldFullPath, e.FullPath);

        if (ignoreRules.IsExcluded(e.FullPath, watchedRoots))
        {
            // Renamed to an excluded name — fire a list_changed so agents know the old entry is gone
            logger.LogDebug("Renamed file matches exclusion, suppressing updated event: {Path}", e.FullPath);
            RaiseFileChanged(e.OldFullPath, WatcherChangeTypes.Deleted);
            return;
        }

        FileChanged?.Invoke(this, new FileChangedEventArgs
        {
            AbsolutePath = e.FullPath,
            ChangeType = WatcherChangeTypes.Renamed,
            OldPath = e.OldFullPath
        });
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "FileSystemWatcher encountered an error.");
    }

    private void RaiseFileChanged(string path, WatcherChangeTypes changeType)
    {
        FileChanged?.Invoke(this, new FileChangedEventArgs
        {
            AbsolutePath = path,
            ChangeType = changeType
        });
    }

    public void Dispose()
    {
        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}
