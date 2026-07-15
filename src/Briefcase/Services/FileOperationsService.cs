using Briefcase.Configuration;
using Briefcase.Notifications;
using Briefcase.Registry;
using Briefcase.Services.Trash;

namespace Briefcase.Services;

// Web-only file operations (move, delete, project assignment). Move and delete are deliberately
// not exposed as MCP tools — agents keep the tools they have today (archive_file is the only
// agent-facing removal mechanism). Project assignment mirrors create_project/add_file_to_project,
// which are already agent-accessible, so it carries no extra safety concerns.
public class FileOperationsService
{
    private readonly FileRegistry fileRegistry;
    private readonly ProjectRegistry projectRegistry;
    private readonly AppSettings appSettings;
    private readonly NotificationDispatcher notificationDispatcher;
    private readonly ITrashService trashService;

    public FileOperationsService(
        FileRegistry fileRegistry,
        ProjectRegistry projectRegistry,
        AppSettings appSettings,
        NotificationDispatcher notificationDispatcher,
        ITrashService trashService)
    {
        this.fileRegistry = fileRegistry;
        this.projectRegistry = projectRegistry;
        this.appSettings = appSettings;
        this.notificationDispatcher = notificationDispatcher;
        this.trashService = trashService;
    }

    public async Task<string?> MoveFile(Guid id, string destinationRoot, string? subfolder)
    {
        var entry = fileRegistry.GetById(id);
        if (entry is null)
            return "File not found.";

        var normalizedRoot = appSettings.BriefcasePaths
            .FirstOrDefault(root => PathsEqual(root, destinationRoot));
        if (normalizedRoot is null)
            return "Destination is not one of the configured Briefcase directories.";

        var destinationDir = Path.GetFullPath(normalizedRoot);
        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            destinationDir = Path.GetFullPath(Path.Combine(destinationDir, subfolder));
            if (!IsWithinRoot(destinationDir, normalizedRoot))
                return "Subfolder must stay within the destination directory.";
        }

        if (!File.Exists(entry.AbsolutePath))
            return $"File '{entry.Name}' is registered but no longer exists on disk.";

        var destinationPath = Path.Combine(destinationDir, entry.Name);
        if (string.Equals(Path.GetFullPath(destinationPath), Path.GetFullPath(entry.AbsolutePath), StringComparison.OrdinalIgnoreCase))
            return "File is already at that location.";

        if (File.Exists(destinationPath))
            return $"A file named '{entry.Name}' already exists at the destination.";

        try
        {
            Directory.CreateDirectory(destinationDir);
            File.Move(entry.AbsolutePath, destinationPath);
        }
        catch (Exception ex)
        {
            return $"Failed to move file '{entry.Name}': {ex.Message}";
        }

        fileRegistry.Rename(entry.AbsolutePath, destinationPath);
        await notificationDispatcher.SendListChangedAsync();
        return null;
    }

    public async Task<string?> DeleteFile(Guid id)
    {
        var entry = fileRegistry.GetById(id);
        if (entry is null)
            return "File not found.";

        if (File.Exists(entry.AbsolutePath))
        {
            try
            {
                trashService.Trash(entry.AbsolutePath);
            }
            catch (Exception ex)
            {
                return $"Failed to delete file '{entry.Name}': {ex.Message}";
            }
        }

        fileRegistry.Remove(entry.AbsolutePath);
        projectRegistry.PruneFileId(id);
        await notificationDispatcher.SendListChangedAsync();
        return null;
    }

    // Assigns a file to a project. Pass exactly one of existingProjectId or newProjectName.
    public async Task<string?> AssignProject(Guid fileId, Guid? existingProjectId, string? newProjectName)
    {
        if (fileRegistry.GetById(fileId) is null)
            return "File not found.";

        Guid projectId;
        if (existingProjectId.HasValue)
        {
            if (projectRegistry.GetById(existingProjectId.Value) is null)
                return "Project not found.";
            projectId = existingProjectId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(newProjectName))
        {
            var (entry, error) = projectRegistry.Create(newProjectName.Trim(), string.Empty);
            if (error != null)
                return error;
            projectId = entry!.Id;
        }
        else
        {
            return "Choose an existing project or enter a name for a new one.";
        }

        projectRegistry.AddFile(projectId, fileId);
        await notificationDispatcher.SendProjectListChangedAsync();
        return null;
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinRoot(string candidate, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCandidate, normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
