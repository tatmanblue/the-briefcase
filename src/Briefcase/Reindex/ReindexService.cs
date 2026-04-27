using Briefcase.Notifications;
using Briefcase.Registry;
using Briefcase.Search;
using Microsoft.Extensions.Logging;

namespace Briefcase.Reindex;

public class ReindexService
{
    private readonly FileRegistry registry;
    private readonly SearchCache searchCache;
    private readonly NotificationDispatcher notificationDispatcher;
    private readonly ILogger<ReindexService> logger;
    private int reindexing = 0;

    public ReindexService(
        FileRegistry registry,
        SearchCache searchCache,
        NotificationDispatcher notificationDispatcher,
        ILogger<ReindexService> logger)
    {
        this.registry = registry;
        this.searchCache = searchCache;
        this.notificationDispatcher = notificationDispatcher;
        this.logger = logger;
    }

    public async Task<ReindexResult> RunAsync()
    {
        if (Interlocked.CompareExchange(ref reindexing, 1, 0) != 0)
            return ReindexResult.AlreadyRunning;

        try
        {
            logger.LogInformation("Reindex started.");
            var (added, pruned) = await Task.Run(() => registry.Reindex());

            int cacheBuilt = 0;
            if (searchCache.IsEnabled)
                cacheBuilt = await Task.Run(() => searchCache.Rebuild(registry.GetAll()));

            if (added > 0 || pruned > 0)
                await notificationDispatcher.SendListChangedAsync();

            logger.LogInformation(
                "Reindex completed: {Added} added, {Pruned} pruned, {CacheBuilt} cache entries built.",
                added, pruned, cacheBuilt);

            return new ReindexResult
            {
                Status = "completed",
                RegistryAdded = added,
                RegistryPruned = pruned,
                CacheEntriesBuilt = cacheBuilt
            };
        }
        finally
        {
            Interlocked.Exchange(ref reindexing, 0);
        }
    }
}

public class ReindexResult
{
    public static readonly ReindexResult AlreadyRunning = new() { Status = "already_running" };

    public string Status { get; init; } = string.Empty;
    public int RegistryAdded { get; init; }
    public int RegistryPruned { get; init; }
    public int CacheEntriesBuilt { get; init; }
}
