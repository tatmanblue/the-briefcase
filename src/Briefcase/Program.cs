using Briefcase.Configuration;
using Briefcase.Exclusions;
using Briefcase.Notifications;
using Briefcase.Reindex;
using Briefcase.Registry;
using Briefcase.Search;
using Briefcase.Services;
using Briefcase.Services.Content;
using Briefcase.Services.Trash;
using Briefcase.Tools;
using Briefcase.Watching;
using Briefcase.Web.Components;
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
// Paths are canonicalized (Path.GetFullPath) so downstream comparisons (FileRegistry, FileWatcher,
// IgnoreRules) are never fooled by differently-formatted strings pointing at the same directory
// (e.g. mixed slash styles), which previously caused duplicate registry entries.
var briefcasePaths = (Environment.GetEnvironmentVariable("BRIEFCASE_PATHS") ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(Path.GetFullPath)
    .ToArray();

var dataPath = Environment.GetEnvironmentVariable("BRIEFCASE_DATA_PATH") ?? string.Empty;

var newPath = Environment.GetEnvironmentVariable("BRIEFCASE_NEW_PATH")
    ?? (briefcasePaths.Length > 0 ? Path.Combine(briefcasePaths[0], "new") : string.Empty);

if (!string.IsNullOrEmpty(newPath))
{
    newPath = Path.GetFullPath(newPath);
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

var webPort = int.TryParse(Environment.GetEnvironmentVariable("BRIEFCASE_WEB_PORT"), out var parsedWebPort)
    && parsedWebPort is > 0 and <= 65535
    ? parsedWebPort
    : 5289;

var appSettings = new AppSettings
{
    BriefcasePaths = briefcasePaths,
    DataPath = dataPath,
    NewPath = newPath,
    IgnoreFilePath = ignoreFilePath,
    ListFilesDefaultLimit = listFilesDefaultLimit,
    SearchDefaultLimit = searchDefaultLimit,
    SearchMaxFileSizeKb = searchMaxFileSizeKb,
    SearchCacheEnabled = searchCacheEnabled,
    WebPort = webPort
};

// Validate configuration and exit with a clear message if anything is wrong.
if (!StartupValidator.Validate(appSettings, envFileLoaded))
    Environment.Exit(1);

// The MCP stdio transport talks to the raw OS stdout handle directly (Console.OpenStandardOutput),
// bypassing Console.Out entirely. Redirecting Console.Out here is free for the MCP stream and
// catches any stray Console.WriteLine from ASP.NET Core internals or a third-party dependency.
Console.SetOut(Console.Error);

// ContentRootPath (and the WebRootPath derived from it) defaults to the process's current working
// directory, which is unpredictable when an MCP client launches this exe directly — it's whatever
// CWD the client happens to use, not necessarily this exe's own folder. Pin it explicitly so static
// web assets (wwwroot) always resolve relative to where the exe/wwwroot actually live.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.WebHost.UseUrls($"http://127.0.0.1:{appSettings.WebPort}");

// Suppress the "Now listening on..." / "Application started..." banner — it writes straight to
// stdout via ConsoleLifetime and would corrupt the MCP JSON-RPC stream on stdout.
builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);

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
builder.Services.AddSingleton<FileQueryService>();
builder.Services.AddSingleton<FileOperationsService>();
builder.Services.AddSingleton<ContentRenderer>();

if (OperatingSystem.IsWindows())
    builder.Services.AddSingleton<ITrashService, WindowsTrashService>();
else if (OperatingSystem.IsMacOS())
    builder.Services.AddSingleton<ITrashService, MacTrashService>();
else if (OperatingSystem.IsLinux())
    builder.Services.AddSingleton<ITrashService, LinuxTrashService>();
else
    builder.Services.AddSingleton<ITrashService, NoOpTrashService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
    .WithTools<DeleteProjectTool>()
    .WithTools<ArchiveFileTool>()
    .WithTools<UnarchiveFileTool>();

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
