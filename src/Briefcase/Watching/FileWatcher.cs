using Briefcase.Configuration;
using Briefcase.Registry;
using Microsoft.Extensions.Logging;

namespace Briefcase.Watching;

public class FileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly FileRegistry registry;
    private readonly ILogger<FileWatcher> logger;

    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public FileWatcher(AppSettings settings, FileRegistry registry, ILogger<FileWatcher> logger)
    {
        this.registry = registry;
        this.logger = logger;

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
        registry.AddOrUpdate(e.FullPath);
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Created);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Changed);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        registry.Remove(e.FullPath);
        RaiseFileChanged(e.FullPath, WatcherChangeTypes.Deleted);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (Directory.Exists(e.FullPath)) return;
        registry.Rename(e.OldFullPath, e.FullPath);
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
