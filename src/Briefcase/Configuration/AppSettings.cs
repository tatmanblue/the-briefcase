namespace Briefcase.Configuration;

public class AppSettings
{
    public string[] BriefcasePaths { get; init; } = [];
    public string DataPath { get; init; } = string.Empty;
    public string NewPath { get; init; } = string.Empty;
    public string IgnoreFilePath { get; init; } = string.Empty;
    // Negative value means no limit (return all files).
    public int ListFilesDefaultLimit { get; init; } = -1;
    // Negative value means no limit (return all matches).
    public int SearchDefaultLimit { get; init; } = 25;
    // Negative or zero means no size cap.
    public int SearchMaxFileSizeKb { get; init; } = 512;
    public bool SearchCacheEnabled { get; init; } = false;
}
