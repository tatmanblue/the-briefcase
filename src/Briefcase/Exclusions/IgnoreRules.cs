using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Briefcase.Exclusions;

/// <summary>
/// Loads and evaluates a .gitignore-style ignore file to determine whether files
/// should be excluded from the Briefcase registry and change notifications.
///
/// Syntax rules:
///   - Blank lines and lines starting with # are ignored.
///   - Patterns with no '/' are matched against the file name only (e.g. *.tmp).
///   - Patterns containing '/' are matched against the path relative to each watched root (e.g. .git/**).
/// </summary>
public class IgnoreRules
{
    private readonly List<string> namePatterns = [];
    private readonly List<string> pathPatterns = [];
    private readonly ILogger<IgnoreRules> logger;

    public IgnoreRules(string ignoreFilePath, ILogger<IgnoreRules> logger)
    {
        this.logger = logger;
        Load(ignoreFilePath);
    }

    public bool IsExcluded(string absolutePath, string[] watchedRoots)
    {
        if (namePatterns.Count == 0 && pathPatterns.Count == 0)
            return false;

        var fileName = Path.GetFileName(absolutePath);

        if (namePatterns.Count > 0)
        {
            var nameMatcher = new Matcher();
            nameMatcher.AddIncludePatterns(namePatterns);
            if (nameMatcher.Match(fileName).HasMatches)
                return true;
        }

        if (pathPatterns.Count > 0)
        {
            var pathMatcher = new Matcher();
            pathMatcher.AddIncludePatterns(pathPatterns);

            foreach (var root in watchedRoots)
            {
                if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = Path.GetRelativePath(root, absolutePath)
                    .Replace('\\', '/');

                if (pathMatcher.Match(relativePath).HasMatches)
                    return true;
            }
        }

        return false;
    }

    private void Load(string ignoreFilePath)
    {
        if (!File.Exists(ignoreFilePath))
        {
            logger.LogInformation("No ignore file found at {Path}. No exclusions will be applied.", ignoreFilePath);
            return;
        }

        var lines = File.ReadAllLines(ignoreFilePath);
        var loaded = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.Contains('/'))
            {
                var pattern = line.TrimStart('/');
                if (pattern.EndsWith('/'))
                    pattern += "**";
                pathPatterns.Add(pattern);
            }
            else
                namePatterns.Add(line);

            loaded++;
        }

        logger.LogInformation("Loaded {Count} exclusion pattern(s) from {Path}.", loaded, ignoreFilePath);
    }
}
