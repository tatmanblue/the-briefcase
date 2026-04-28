namespace Briefcase.Registry;

public class ProjectEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; init; } = DateTime.UtcNow;
    public List<Guid> FileIds { get; set; } = [];
}
