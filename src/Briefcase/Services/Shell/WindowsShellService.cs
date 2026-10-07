using System.Diagnostics;

namespace Briefcase.Services.Shell;

public class WindowsShellService : IShellService
{
    public bool IsSupported => true;

    public void Open(string absolutePath)
    {
        Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true })?.Dispose();
    }

    // explorer's /select switch takes the path in the same argument ("/select,<path>"), so it
    // can't go through ArgumentList; quote it ourselves. Windows paths can't contain '"'.
    public void ShowInFolder(string absolutePath)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{absolutePath}\"")
        {
            UseShellExecute = false
        })?.Dispose();
    }
}
