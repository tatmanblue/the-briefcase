using System.Diagnostics;

namespace Briefcase.Services.Shell;

public class MacShellService : IShellService
{
    public bool IsSupported => true;

    public void Open(string absolutePath) => Run("open", absolutePath);

    // -R reveals the file in Finder instead of opening it.
    public void ShowInFolder(string absolutePath) => Run("open", "-R", absolutePath);

    private static void Run(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        Process.Start(startInfo)?.Dispose();
    }
}
