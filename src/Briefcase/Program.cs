using Briefcase.Configuration;
using Briefcase.Exclusions;
using Briefcase.Notifications;
using Briefcase.Reindex;
using Briefcase.Registry;
using Briefcase.Search;
using Briefcase.Tools;
using Briefcase.Watching;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Load .env — try the working directory first, then the executable directory.
// Track which file was loaded so the validator can report it clearly.
string? envFileLoaded = null;
var cwdEnvFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
var baseEnvFile = Path.Combine(AppContext.BaseDirectory, ".env");

if (File.Exists(cwdEnvFile))
{
    Env.Load(cwdEnvFile);
    envFileLoaded = cwdEnvFile;
}
else if (File.Exists(baseEnvFile))
{
    Env.Load(baseEnvFile);
    envFileLoaded = baseEnvFile;
}

// Read environment variables — no throwing here; validation reports problems clearly.
var briefcasePaths = (Environment.GetEnvironmentVariable("BRIEFCASE_PATHS") ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var dataPath = Environment.GetEnvironmentVariable("BRIEFCASE_DATA_PATH") ?? string.Empty;

var newPath = Environment.GetEnvironmentVariable("BRIEFCASE_NEW_PATH")
    ?? (briefcasePaths.Length > 0 ? Path.Combine(briefcasePaths[0], "new") : string.Empty);

if (!string.IsNullOrEmpty(newPath))
{
    Directory.CreateDirectory(newPath);
    if (!briefcasePaths.Contains(newPath, StringComparer.OrdinalIgnoreCase))
        briefcasePaths = [.. briefcasePaths, newPath];
}

var ignoreFilePath = Environment.GetEnvironmentVariable("BRIEFCASE_IGNORE_FILE")
    ?? (string.IsNullOrEmpty(dataPath) ? string.Empty : Path.Combine(dataPath, ".briefcase-ignore"));

var listFilesDefaultLimit = int.TryParse(
    Environment.GetEnvironmentVariable("BRIEFCASE_LIST_DEFAULT_LIMIT"), out var parsedLimit)
    ? parsedLimit
    : -1;

var searchDefaultLimit = int.TryParse(
    Environment.GetEnvironmentVariable("BRIEFCASE_SEARCH_DEFAULT_LIMIT"), out var parsedSearchLimit)
    ? parsedSearchLimit
    : 25;

var searchMaxFileSizeKb = int.TryParse(
    Environment.GetEnvironmentVariable("BRIEFCASE_SEARCH_MAX_FILE_SIZE_KB"), out var parsedSearchMaxSize)
    ? parsedSearchMaxSize
    : 512;

var searchCacheEnabled = string.Equals(
    Environment.GetEnvironmentVariable("BRIEFCASE_SEARCH_CACHE_ENABLED"), "true",
    StringComparison.OrdinalIgnoreCase);

var appSettings = new AppSettings
{
    BriefcasePaths = briefcasePaths,
    DataPath = dataPath,
    NewPath = newPath,
    IgnoreFilePath = ignoreFilePath,
    ListFilesDefaultLimit = listFilesDefaultLimit,
    SearchDefaultLimit = searchDefaultLimit,
    SearchMaxFileSizeKb = searchMaxFileSizeKb,
    SearchCacheEnabled = searchCacheEnabled
};

// Validate configuration and exit with a clear message if anything is wrong.
if (!StartupValidator.Validate(appSettings, envFileLoaded))
    Environment.Exit(1);

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<IgnoreRules>(sp =>
    new IgnoreRules(appSettings.IgnoreFilePath, sp.GetRequiredService<ILogger<IgnoreRules>>()));
builder.Services.AddSingleton<FileRegistry>();
builder.Services.AddSingleton<ProjectRegistry>();
builder.Services.AddSingleton<FileWatcher>();
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcher>());
builder.Services.AddSingleton<SearchCache>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SearchCache>());
builder.Services.AddSingleton<ReindexService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ListFilesTool>()
    .WithTools<ReadFileTool>()
    .WithTools<CreateFileTool>()
    .WithTools<UpdateFileTool>()
    .WithTools<SearchFilesTool>()
    .WithTools<ReindexTool>()
    .WithTools<CreateProjectTool>()
    .WithTools<ListProjectsTool>()
    .WithTools<GetProjectTool>()
    .WithTools<AddFileToProjectTool>()
    .WithTools<RemoveFileFromProjectTool>()
    .WithTools<UpdateProjectTool>()
    .WithTools<DeleteProjectTool>();

await builder.Build().RunAsync();
