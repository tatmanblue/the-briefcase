using System.ComponentModel;
using System.Text.Json;
using Briefcase.Notifications;
using Briefcase.Registry;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ArchiveFileTool
{
    private readonly FileRegistry fileRegistry;
    private readonly NotificationDispatcher notificationDispatcher;

    public ArchiveFileTool(FileRegistry fileRegistry, NotificationDispatcher notificationDispatcher)
    {
        this.fileRegistry = fileRegistry;
        this.notificationDispatcher = notificationDispatcher;
    }

    [McpServerTool(Name = "archive_file")]
    [Description(
        "Marks a file as archived. Archived files are excluded from list_files and search_files results by default. " +
        "The file remains readable by ID at any time and its project association is preserved. " +
        "Use 'unarchive_file' to restore it to active status. " +
        "Archiving an already-archived file succeeds without error.")]
    public async Task<string> ArchiveFile(
        [Description("The file ID (GUID) returned by list_files.")] Guid id)
    {
        var entry = fileRegistry.Archive(id);
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
                isArchived = true
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
