using Briefcase.Configuration;
using Briefcase.Exclusions;
using Briefcase.Notifications;
using Briefcase.Registry;
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

var newFilesDataPath = Environment.GetEnvironmentVariable("BRIEFCASE_NEW_FILES_DATA_PATH")
    ?? (string.IsNullOrEmpty(dataPath) ? string.Empty : Path.Combine(dataPath, "new"));

if (!string.IsNullOrEmpty(newFilesDataPath))
{
    Directory.CreateDirectory(newFilesDataPath);
    if (!briefcasePaths.Contains(newFilesDataPath, StringComparer.OrdinalIgnoreCase))
        briefcasePaths = [.. briefcasePaths, newFilesDataPath];
}

var ignoreFilePath = Environment.GetEnvironmentVariable("BRIEFCASE_IGNORE_FILE")
    ?? (string.IsNullOrEmpty(dataPath) ? string.Empty : Path.Combine(dataPath, ".briefcase-ignore"));

var appSettings = new AppSettings
{
    BriefcasePaths = briefcasePaths,
    DataPath = dataPath,
    NewFilesDataPath = newFilesDataPath,
    IgnoreFilePath = ignoreFilePath
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
builder.Services.AddSingleton<FileWatcher>();
builder.Services.AddHostedService<NotificationDispatcher>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ListFilesTool>()
    .WithTools<ReadFileTool>()
    .WithTools<CreateFileTool>()
    .WithTools<UpdateFileTool>();

await builder.Build().RunAsync();
