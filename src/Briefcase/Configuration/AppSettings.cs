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
    // Text files larger than this are shown truncated (and not editable) in the web UI.
    // Negative or zero means no size cap.
    public int ViewMaxTextKb { get; init; } = DEFAULT_MAX_FILE_SIZE_KB;
    // Files larger than this are returned by read_file as metadata only. Negative or zero means
    // no size cap.
    public int ReadMaxKb { get; init; } = DEFAULT_MAX_FILE_SIZE_KB;
    // Port the local-only web UI listens on (bound to 127.0.0.1 only).
    public int WebPort { get; init; } = 5289;
    // File extensions the web UI allows creating/editing directly, provided the file is also
    // text-based. Agents are unaffected by this list -- create_file/update_file accept any file,
    // but refuse text content for binary file types (see FileTypeClassifier).
    public string[] EditableExtensions { get; init; } = [".md", ".txt", ".json"];
    // Extensions the web UI will never hand to the OS "open in default app" action, because the
    // OS default action for them is to execute. Show in folder and Download remain available.
    public string[] OpenBlockedExtensions { get; init; } = DEFAULT_OPEN_BLOCKED_EXTENSIONS;

    // 100 MB. Engineering files (logs, data dumps, exports) are routinely large, and a lower cap
    // mostly just forces a human to fetch the file for the agent.
    public const int DEFAULT_MAX_FILE_SIZE_KB = 100 * 1024;

    public static readonly string[] DEFAULT_OPEN_BLOCKED_EXTENSIONS =
    [
        // Windows
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
        ".msi", ".msp", ".scr", ".pif", ".lnk", ".url", ".reg", ".hta", ".cpl", ".jar", ".appref-ms",
        // macOS
        ".app", ".command", ".tool", ".pkg", ".dmg", ".scpt", ".applescript", ".terminal", ".workflow",
        // Linux / Unix
        ".sh", ".bash", ".zsh", ".csh", ".ksh", ".run", ".bin", ".desktop", ".appimage", ".deb", ".rpm",
        // Cross-platform interpreters
        ".py", ".pyw", ".pl", ".rb", ".php"
    ];

    // Converts a KB setting to a byte limit; null means unlimited.
    public static long? ToByteLimit(int kb) => kb > 0 ? kb * 1024L : null;

    public bool IsOpenBlocked(string fileName) =>
        OpenBlockedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
