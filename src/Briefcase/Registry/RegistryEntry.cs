using System.Text.Json.Serialization;

namespace Briefcase.Registry;

public class RegistryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AbsolutePath { get; set; } = string.Empty;

    [JsonIgnore]
    public string Name => Path.GetFileName(AbsolutePath);
}
