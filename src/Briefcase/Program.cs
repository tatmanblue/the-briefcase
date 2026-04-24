using Briefcase.Configuration;
using Briefcase.Notifications;
using Briefcase.Registry;
using Briefcase.Tools;
using Briefcase.Watching;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile)) 
    Env.Load(envFile);
else 
    Env.Load(Path.Combine(AppContext.BaseDirectory, ".env"));

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Build AppSettings from environment variables
var briefcasePaths = (Environment.GetEnvironmentVariable("BRIEFCASE_PATHS") ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var dataPath = Environment.GetEnvironmentVariable("BRIEFCASE_DATA_PATH")
    ?? throw new InvalidOperationException("BRIEFCASE_DATA_PATH environment variable is required.");

var appSettings = new AppSettings
{
    BriefcasePaths = briefcasePaths,
    DataPath = dataPath
};

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<FileRegistry>();
builder.Services.AddSingleton<FileWatcher>();
builder.Services.AddHostedService<NotificationDispatcher>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ListFilesTool>()
    .WithTools<ReadFileTool>();

await builder.Build().RunAsync();
