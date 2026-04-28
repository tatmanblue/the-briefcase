using System.Text.Json;
using Briefcase.Configuration;
using Microsoft.Extensions.Logging;

namespace Briefcase.Registry;

public class ProjectRegistry
{
    private readonly string dataPath;
    private readonly Dictionary<Guid, ProjectEntry> projectsById = new();
    private readonly Dictionary<Guid, Guid> fileToProjectId = new(); // fileId -> projectId
    private readonly object lockObject = new();
    private readonly ILogger<ProjectRegistry> logger;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public ProjectRegistry(AppSettings settings, ILogger<ProjectRegistry> logger)
    {
        this.logger = logger;
        dataPath = settings.DataPath;
        Load();
        logger.LogInformation("ProjectRegistry loaded with {Count} projects.", projectsById.Count);
    }

    public IReadOnlyList<ProjectEntry> GetAll()
    {
        lock (lockObject)
            return projectsById.Values.ToList();
    }

    public ProjectEntry? GetById(Guid id)
    {
        lock (lockObject)
            return projectsById.TryGetValue(id, out var entry) ? entry : null;
    }

    public ProjectEntry? GetByName(string name)
    {
        lock (lockObject)
            return projectsById.Values.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public (Guid projectId, string projectName)? FindProjectForFile(Guid fileId)
    {
        lock (lockObject)
        {
            if (!fileToProjectId.TryGetValue(fileId, out var projectId))
                return null;
            return projectsById.TryGetValue(projectId, out var project)
                ? (project.Id, project.Name)
                : null;
        }
    }

    public (ProjectEntry? project, string? error) Create(string name, string description)
    {
        lock (lockObject)
        {
            if (projectsById.Values.Any(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                return (null, $"A project named '{name}' already exists.");

            var entry = new ProjectEntry
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                CreatedDate = DateTime.UtcNow
            };
            projectsById[entry.Id] = entry;
            Save(entry);
            logger.LogDebug("Created project: {Name} -> {Id}", name, entry.Id);
            return (entry, null);
        }
    }

    public string? Update(Guid id, string? name, string? description)
    {
        lock (lockObject)
        {
            if (!projectsById.TryGetValue(id, out var entry))
                return "Project not found.";

            if (name != null)
            {
                if (projectsById.Values.Any(p =>
                        p.Id != id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return $"A project named '{name}' already exists.";
                entry.Name = name;
            }

            if (description != null)
                entry.Description = description;

            Save(entry);
            return null;
        }
    }

    public bool Delete(Guid projectId)
    {
        lock (lockObject)
        {
            if (!projectsById.TryGetValue(projectId, out var entry))
                return false;

            foreach (var fileId in entry.FileIds)
                fileToProjectId.Remove(fileId);

            projectsById.Remove(projectId);

            var filePath = ProjectFilePath(projectId);
            if (File.Exists(filePath))
                File.Delete(filePath);

            logger.LogDebug("Deleted project: {Id}", projectId);
            return true;
        }
    }

    public bool AddFile(Guid projectId, Guid fileId)
    {
        lock (lockObject)
        {
            if (!projectsById.TryGetValue(projectId, out var entry))
                return false;

            // Remove from any existing project (one file, one project)
            if (fileToProjectId.TryGetValue(fileId, out var existingProjectId) && existingProjectId != projectId)
            {
                if (projectsById.TryGetValue(existingProjectId, out var existingProject))
                {
                    existingProject.FileIds.Remove(fileId);
                    Save(existingProject);
                }
            }

            if (!entry.FileIds.Contains(fileId))
                entry.FileIds.Add(fileId);

            fileToProjectId[fileId] = projectId;
            Save(entry);
            return true;
        }
    }

    public bool RemoveFile(Guid projectId, Guid fileId)
    {
        lock (lockObject)
        {
            if (!projectsById.TryGetValue(projectId, out var entry))
                return false;

            if (!entry.FileIds.Remove(fileId))
                return false;

            fileToProjectId.Remove(fileId);
            Save(entry);
            return true;
        }
    }

    public void PruneFileId(Guid fileId)
    {
        lock (lockObject)
        {
            if (!fileToProjectId.TryGetValue(fileId, out var projectId))
                return;

            fileToProjectId.Remove(fileId);

            if (projectsById.TryGetValue(projectId, out var entry))
            {
                entry.FileIds.Remove(fileId);
                Save(entry);
                logger.LogDebug("Pruned stale file {FileId} from project {ProjectId}.", fileId, projectId);
            }
        }
    }

    private void Load()
    {
        if (!Directory.Exists(dataPath))
            return;

        foreach (var file in Directory.EnumerateFiles(dataPath, "project-*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<ProjectEntry>(json);
                if (entry == null) continue;

                projectsById[entry.Id] = entry;
                foreach (var fileId in entry.FileIds)
                    fileToProjectId[fileId] = entry.Id;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load project file: {Path}", file);
            }
        }
    }

    private void Save(ProjectEntry entry)
    {
        var json = JsonSerializer.Serialize(entry, SerializerOptions);
        File.WriteAllText(ProjectFilePath(entry.Id), json);
    }

    private string ProjectFilePath(Guid id) => Path.Combine(dataPath, $"project-{id}.json");
}
