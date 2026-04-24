namespace Briefcase.Configuration;

public class AppSettings
{
    public string[] BriefcasePaths { get; init; } = [];
    public string DataPath { get; init; } = string.Empty;
    public string IgnoreFilePath { get; init; } = string.Empty;
}
