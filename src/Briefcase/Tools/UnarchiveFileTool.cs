using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class UnarchiveFileTool
{
    private readonly FileRegistry fileRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public UnarchiveFileTool(FileRegistry fileRegistry, NotificationDispatcher notificationDispatcher)
    {
        this.fileRegistry = fileRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "unarchive_file")]
    [Description(
        "Restores an archived file to active status. The file will appear again in list_files and search_files results. " +
        "Project association is preserved through archive and unarchive. " +
        "Unarchiving an already-active file succeeds without error.")]
    public async Task<string> UnarchiveFile(
        [Description("The file ID (GUID) of the archived file.")] Guid id)
    {
        var entry = fileRegistry.Unarchive(id);
        if (entry == null)
            return JsonSerializer.Serialize(new { error = "File not found." });

        await notificationDispatcher.SendListChangedAsync();

        var info = new FileInfo(entry.AbsolutePath);
        return JsonSerializer.Serialize(
            new
            {
                id = entry.Id,
                name = entry.Name,
                size = info.Exists ? info.Length : (long?)null,
                lastModified = info.Exists ? info.LastWriteTimeUtc : (DateTime?)null,
                isArchived = false
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
