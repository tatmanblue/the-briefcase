// Publishes Briefcase and installs it to run in the background on Windows: started at logon,
// restarted automatically if it exits, no console window. The Windows counterpart of
// install-macos.sh. Requires the .NET 10 SDK (already needed to build Briefcase) -- no PowerShell.
//
// Usage (from the repo root, in cmd, Git Bash, or any terminal):
//   dotnet run scripts/install-windows.cs                 publish + install (or update) and start
//   dotnet run scripts/install-windows.cs -- restart      re-copy src\Briefcase\.env and restart, no rebuild
//   dotnet run scripts/install-windows.cs -- stop         stop the server (it starts again at next logon)
//   dotnet run scripts/install-windows.cs -- uninstall    stop, remove the scheduled task and installed files
//
// Layout (%LOCALAPPDATA%\Briefcase):
//   app\        published build + a copy of src\Briefcase\.env (replaced on every install)
//   logs\       briefcase.log (server output)
//   run.cmd     keep-alive loop the scheduled task runs
//
// Settings live in src\Briefcase\.env, which is the only file to edit. Briefcase's data
// (BRIEFCASE_PATHS / BRIEFCASE_DATA_PATH) is never touched by install, restart or uninstall.

using System.Diagnostics;
using System.Net.Http;
using System.Security;
using System.Text;

const string TASK_NAME = "Briefcase";
const string DEFAULT_PORT = "5289";
const long MAX_LOG_BYTES = 10 * 1024 * 1024;

if (!OperatingSystem.IsWindows())
    return Fail("This installer is for Windows. On macOS use scripts/install-macos.sh.");

var repoRoot = FindRepoRoot();
if (repoRoot is null)
    return Fail("Could not find the repo root (Briefcase.slnx). Run this from inside the repo.");

var projectPath = Path.Combine(repoRoot, "src", "Briefcase", "Briefcase.csproj");
var envSource = Path.Combine(repoRoot, "src", "Briefcase", ".env");
var publishDir = Path.Combine(repoRoot, "publish");

var installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Briefcase");
var appDir = Path.Combine(installRoot, "app");
var logDir = Path.Combine(installRoot, "logs");
var logFile = Path.Combine(logDir, "briefcase.log");
var runnerPath = Path.Combine(installRoot, "run.cmd");
var stopFlagPath = Path.Combine(installRoot, "stop.flag");
var exePath = Path.Combine(appDir, "Briefcase.exe");

var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "install";
return command switch
{
    "install" => Install(),
    "restart" => Restart(),
    "stop" => StopCommand(),
    "uninstall" => Uninstall(),
    _ => Fail($"Unknown command '{args[0]}'. Use install (default), restart, stop, or uninstall.")
};

int Install()
{
    if (!File.Exists(envSource))
        return Fail($"{envSource} not found.\n" +
                    "Copy src\\Briefcase\\.env.example to src\\Briefcase\\.env and fill in BRIEFCASE_PATHS / " +
                    "BRIEFCASE_DATA_PATH first (see docs/setup-windows.md).");

    // Publish to the repo's staging folder first; the running server is only stopped once a build
    // has succeeded, so a broken build never takes down a working install.
    Step("Publishing self-contained build (win-x64)");
    if (Run("dotnet", "publish", projectPath, "-r", "win-x64", "-o", publishDir) != 0)
        return Fail("dotnet publish failed; the existing install (if any) was left running.");

    StopServer();

    Step($"Installing to {appDir}");
    if (Directory.Exists(appDir))
        Directory.Delete(appDir, recursive: true);
    CopyDirectory(publishDir, appDir);
    File.Copy(envSource, Path.Combine(appDir, ".env"), overwrite: true);

    WriteRunner();
    if (!RegisterTask())
        return Fail("Could not register the scheduled task.");

    return StartAndVerify();
}

int Restart()
{
    if (!File.Exists(exePath))
        return Fail("Briefcase is not installed yet. Run without arguments to install.");
    if (!File.Exists(envSource))
        return Fail($"{envSource} not found.");

    StopServer();
    Step("Copying src\\Briefcase\\.env");
    File.Copy(envSource, Path.Combine(appDir, ".env"), overwrite: true);
    return StartAndVerify();
}

int StopCommand()
{
    if (!File.Exists(exePath))
        return Fail("Briefcase is not installed.");

    StopServer();
    Console.WriteLine("Stopped. It will start again at your next logon (or run with 'restart').");
    return 0;
}

int Uninstall()
{
    StopServer();
    Step($"Removing scheduled task '{TASK_NAME}'");
    Run("schtasks", "/Delete", "/TN", TASK_NAME, "/F");

    Step($"Removing {installRoot}");
    if (Directory.Exists(installRoot))
        Directory.Delete(installRoot, recursive: true);

    Console.WriteLine("Uninstalled. Your Briefcase files and data directory were not touched.");
    return 0;
}

// Stopping can't rely on 'schtasks /End': it ends the task's own process but leaves its children
// (the run.cmd loop and Briefcase.exe) running. Instead, drop a stop flag the loop checks, kill the
// Briefcase.exe running from the install folder (and only that one -- a dev copy run from the repo
// is left alone), then wait for the loop to acknowledge by deleting the flag.
void StopServer()
{
    // The task also counts as running while the loop sits in its restart delay after a crash, with
    // no Briefcase.exe alive -- it must still be told to stop, or it relaunches mid-install.
    var running = InstalledProcesses();
    if (running.Count == 0 && !TaskIsRunning())
    {
        DeleteIfExists(stopFlagPath);
        return;
    }

    Step("Stopping the running server");
    Directory.CreateDirectory(installRoot);
    File.WriteAllText(stopFlagPath, "stop");

    foreach (var process in running)
    {
        try
        {
            process.Kill();
            process.WaitForExit(10_000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    warning: could not stop PID {process.Id}: {ex.Message}");
        }
    }

    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (File.Exists(stopFlagPath) && DateTime.UtcNow < deadline)
        Thread.Sleep(250);

    // Not acknowledged: the server wasn't started by run.cmd (e.g. launched by hand). Nothing is
    // left to restart it, so just clear the flag.
    DeleteIfExists(stopFlagPath);
}

static bool TaskIsRunning()
{
    var startInfo = new ProcessStartInfo("schtasks")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in new[] { "/Query", "/TN", TASK_NAME, "/FO", "CSV", "/NH" })
        startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo)!;
    var output = process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode == 0 && output.Contains("\"Running\"", StringComparison.OrdinalIgnoreCase);
}

List<Process> InstalledProcesses()
{
    var result = new List<Process>();
    foreach (var process in Process.GetProcessesByName("Briefcase"))
    {
        try
        {
            var path = process.MainModule?.FileName;
            if (path != null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase))
                result.Add(process);
        }
        catch
        {
            // Process exited, or belongs to another user -- not ours either way.
        }
    }
    return result;
}

// Keep-alive loop: restarts Briefcase 5 seconds after it exits, unless the installer asked it to
// stop. Only rewritten when its content changes -- cmd reads a running batch file incrementally,
// so rewriting it under a live loop could corrupt that loop.
void WriteRunner()
{
    var runner =
        "@echo off\r\n" +
        "rem Generated by scripts/install-windows.cs -- keeps Briefcase running; restarts it if it exits.\r\n" +
        "set \"BASE=%~dp0\"\r\n" +
        ":loop\r\n" +
        "\"%BASE%app\\Briefcase.exe\" >> \"%BASE%logs\\briefcase.log\" 2>&1\r\n" +
        "if exist \"%BASE%stop.flag\" (del \"%BASE%stop.flag\" & exit /b 0)\r\n" +
        "ping -n 6 127.0.0.1 >nul\r\n" +
        "if exist \"%BASE%stop.flag\" (del \"%BASE%stop.flag\" & exit /b 0)\r\n" +
        "goto loop\r\n";

    Directory.CreateDirectory(logDir);
    if (!File.Exists(runnerPath) || File.ReadAllText(runnerPath) != runner)
        File.WriteAllText(runnerPath, runner);
}

// Registered from XML (not schtasks flags) because only XML exposes the settings needed: no
// execution time limit, keep running on battery, and no-window launch via 'conhost --headless'.
// Runs as the current user in their desktop session, so "Open in default app", "Show in folder"
// and the Recycle Bin behave exactly as when Briefcase is started by hand. No admin rights needed.
bool RegisterTask()
{
    Step($"Registering scheduled task '{TASK_NAME}' (runs at logon)");

    var user = SecurityElement.Escape(Environment.UserDomainName + "\\" + Environment.UserName);
    var arguments = SecurityElement.Escape($"--headless cmd.exe /c \"{runnerPath}\"");
    var workingDir = SecurityElement.Escape(appDir);

    var xml = $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Briefcase MCP server (installed by scripts/install-windows.cs)</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
              <UserId>{user}</UserId>
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{user}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Enabled>true</Enabled>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>conhost.exe</Command>
              <Arguments>{arguments}</Arguments>
              <WorkingDirectory>{workingDir}</WorkingDirectory>
            </Exec>
          </Actions>
        </Task>
        """;

    var xmlPath = Path.Combine(Path.GetTempPath(), $"briefcase-task-{Guid.NewGuid():N}.xml");
    try
    {
        File.WriteAllText(xmlPath, xml, Encoding.Unicode);
        return Run("schtasks", "/Create", "/TN", TASK_NAME, "/XML", xmlPath, "/F") == 0;
    }
    finally
    {
        DeleteIfExists(xmlPath);
    }
}

int StartAndVerify()
{
    RotateLog();
    DeleteIfExists(stopFlagPath);

    Step("Starting");
    if (Run("schtasks", "/Run", "/TN", TASK_NAME) != 0)
        return Fail($"Could not start the scheduled task '{TASK_NAME}'. Is it installed?");

    var port = ReadEnvValue("BRIEFCASE_WEB_PORT") ?? DEFAULT_PORT;
    var url = $"http://127.0.0.1:{port}/mcp";
    Step($"Waiting for {url}");

    if (!WaitForServer(url, TimeSpan.FromSeconds(30)))
    {
        Console.WriteLine();
        Console.WriteLine($"Briefcase did not respond on port {port}. Last lines of {logFile}:");
        PrintLogTail(20);
        Console.WriteLine();
        Console.WriteLine($"If the port is in use by another Briefcase (e.g. one started by hand), stop that one and run with 'restart'.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Briefcase is running.");
    Console.WriteLine($"    Web UI:  http://127.0.0.1:{port}");
    Console.WriteLine($"    MCP:     {url}");
    Console.WriteLine($"    Logs:    {logFile}");
    Console.WriteLine($"    Task:    schtasks /Query /TN {TASK_NAME}");
    Console.WriteLine("    After editing src\\Briefcase\\.env:  dotnet run scripts/install-windows.cs -- restart");
    return 0;
}

static bool WaitForServer(string url, TimeSpan timeout)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"install-windows","version":"1"}}}""",
                    Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            using var response = client.Send(request);
            if (response.IsSuccessStatusCode)
                return true;
        }
        catch
        {
            // Not listening yet.
        }
        Thread.Sleep(1000);
    }
    return false;
}

// Keeps briefcase.log from growing forever: once it passes MAX_LOG_BYTES, the previous log is
// kept as briefcase.old.log. Only done while the server is stopped (the loop holds the file open).
void RotateLog()
{
    if (!File.Exists(logFile) || new FileInfo(logFile).Length < MAX_LOG_BYTES)
        return;
    var oldLog = Path.Combine(logDir, "briefcase.old.log");
    DeleteIfExists(oldLog);
    File.Move(logFile, oldLog);
}

void PrintLogTail(int lines)
{
    if (!File.Exists(logFile))
    {
        Console.WriteLine("    (no log written yet)");
        return;
    }
    using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var reader = new StreamReader(stream);
    var all = reader.ReadToEnd().Split('\n');
    foreach (var line in all.Skip(Math.Max(0, all.Length - lines)))
        Console.WriteLine("    " + line.TrimEnd('\r'));
}

string? ReadEnvValue(string key)
{
    var envPath = Path.Combine(appDir, ".env");
    if (!File.Exists(envPath))
        return null;
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith(key + "=", StringComparison.Ordinal))
        {
            var value = trimmed[(key.Length + 1)..].Trim().Trim('"');
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
    return null;
}

static string? FindRepoRoot()
{
    // File-based apps expose the script's own directory; fall back to searching up from the
    // current directory.
    var start = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Directory.GetCurrentDirectory();
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Briefcase.slnx")))
            return dir.FullName;
    }
    return null;
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static int Run(string fileName, params string[] arguments)
{
    var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo)!;
    process.WaitForExit();
    return process.ExitCode;
}

static void DeleteIfExists(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static void Step(string message) => Console.WriteLine($"==> {message}");

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}
