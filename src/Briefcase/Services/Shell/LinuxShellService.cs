using System.Diagnostics;

namespace Briefcase.Services.Shell;

public class LinuxShellService : IShellService
{
    public bool IsSupported => true;

    public void Open(string absolutePath) => Run("xdg-open", absolutePath)?.Dispose();

    // There is no universal "reveal" command on Linux. The freedesktop FileManager1 D-Bus
    // interface (Nautilus, Dolphin, Nemo, Caja, ...) highlights the file; if that isn't available,
    // fall back to opening the containing folder.
    public void ShowInFolder(string absolutePath)
    {
        try
        {
            var fileUri = new Uri(absolutePath).AbsoluteUri;
            using var process = Run(
                "dbus-send", "--session", "--print-reply", "--dest=org.freedesktop.FileManager1",
                "/org/freedesktop/FileManager1", "org.freedesktop.FileManager1.ShowItems",
                $"array:string:{fileUri}", "string:");
            if (process != null && process.WaitForExit(3000) && process.ExitCode == 0)
                return;
        }
        catch
        {
            // dbus-send missing or no session bus -- fall through to the folder fallback.
        }

        Run("xdg-open", Path.GetDirectoryName(absolutePath)!)?.Dispose();
    }

    private static Process? Run(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo);
    }
}
