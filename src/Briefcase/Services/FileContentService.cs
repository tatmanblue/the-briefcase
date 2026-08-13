using Briefcase.Configuration;
using Briefcase.Registry;

namespace Briefcase.Services;

// Shared write path for file content mutation, used by both the MCP tools (update_file,
// create_file) and the web UI's edit/create pages, so agents and the browser always see the same
// validation and on-disk behavior.
public class FileContentService
{
    private readonly FileRegistry registry;
    private readonly ProjectRegistry projectRegistry;
    private readonly AppSettings settings;

    public FileContentService(FileRegistry registry, ProjectRegistry projectRegistry, AppSettings settings)
    {
        this.registry = registry;
        this.projectRegistry = projectRegistry;
        this.settings = settings;
    }

    // ifUnmodifiedSinceUtc is an optional optimistic-concurrency guard: if supplied and the file's
    // on-disk last-write time no longer matches it, the write is refused as a conflict rather than
    // silently overwriting whatever changed it in the meantime (e.g. an agent's update_file call
    // made while a human had the file open for editing in the web UI). MCP's update_file passes
    // null here and keeps the existing always-overwrite behavior.
    public UpdateFileResult UpdateFile(Guid id, string content, DateTime? ifUnmodifiedSinceUtc = null)
    {
        var entry = registry.GetById(id);
        if (entry is null)
            return new UpdateFileResult(false, $"No file found with ID '{id}'.", false, null, null, null, null);

        if (!File.Exists(entry.AbsolutePath))
            return new UpdateFileResult(false, $"File '{entry.Name}' is registered but no longer exists on disk.", false, null, null, null, null);

        if (ifUnmodifiedSinceUtc.HasValue && File.GetLastWriteTimeUtc(entry.AbsolutePath) != ifUnmodifiedSinceUtc.Value)
            return new UpdateFileResult(false, $"File '{entry.Name}' was changed since it was loaded and cannot be saved.", true, null, null, null, null);

        try
        {
            File.WriteAllText(entry.AbsolutePath, content);
        }
        catch (Exception ex)
        {
            return new UpdateFileResult(false, $"Failed to write file '{entry.Name}': {ex.Message}", false, null, null, null, null);
        }

        var info = new FileInfo(entry.AbsolutePath);
        return new UpdateFileResult(true, null, false, entry.Id, entry.Name, info.Length, info.LastWriteTimeUtc);
    }

    public CreateFileResult CreateFile(string name, string content, Guid? projectId = null)
    {
        if (string.IsNullOrEmpty(settings.NewPath))
            return new CreateFileResult(false, "New-files directory is not configured (BRIEFCASE_NEW_PATH).", null, null, null, null, null);

        if (projectId.HasValue && projectRegistry.GetById(projectId.Value) == null)
            return new CreateFileResult(false, $"Project '{projectId.Value}' not found.", null, null, null, null, null);

        var safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            return new CreateFileResult(false, "Invalid file name.", null, null, null, null, null);

        var absolutePath = Path.Combine(settings.NewPath, safeName);

        if (File.Exists(absolutePath))
            return new CreateFileResult(false, $"A file named '{safeName}' already exists.", null, null, null, null, null);

        try
        {
            File.WriteAllText(absolutePath, content);
        }
        catch (Exception ex)
        {
            return new CreateFileResult(false, $"Failed to write file '{safeName}': {ex.Message}", null, null, null, null, null);
        }

        var fileId = registry.AddOrUpdate(absolutePath);
        var info = new FileInfo(absolutePath);

        if (projectId.HasValue && fileId.HasValue)
            projectRegistry.AddFile(projectId.Value, fileId.Value);

        return new CreateFileResult(true, null, fileId, safeName, info.Length, info.LastWriteTimeUtc, projectId);
    }
}

public record UpdateFileResult(bool Success, string? Error, bool Conflict, Guid? Id, string? Name, long? Size, DateTime? LastModifiedUtc);

public record CreateFileResult(bool Success, string? Error, Guid? Id, string? Name, long? Size, DateTime? LastModifiedUtc, Guid? ProjectId);
