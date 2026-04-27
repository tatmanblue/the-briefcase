using System.ComponentModel;
using System.Text.Json;
using Briefcase.Reindex;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ReindexTool
{
    private readonly ReindexService reindexService;

    public ReindexTool(ReindexService reindexService)
    {
        this.reindexService = reindexService;
    }

    [McpServerTool(Name = "reindex_files")]
    [Description(
        "Rebuilds the Briefcase registry and search cache. " +
        "Scans all configured directories to add newly discovered files and prune stale entries. " +
        "If the search cache is enabled, rebuilds it fully so subsequent searches are fast. " +
        "Blocks until the reindex is complete. " +
        "If a reindex is already in progress, returns immediately with status 'already_running'.")]
    public async Task<string> ReindexFiles()
    {
        var result = await reindexService.RunAsync();

        if (result.Status == "already_running")
            return JsonSerializer.Serialize(
                new { status = result.Status },
                new JsonSerializerOptions { WriteIndented = true });

        return JsonSerializer.Serialize(
            new
            {
                status = result.Status,
                registryAdded = result.RegistryAdded,
                registryPruned = result.RegistryPruned,
                cacheEntriesBuilt = result.CacheEntriesBuilt
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
