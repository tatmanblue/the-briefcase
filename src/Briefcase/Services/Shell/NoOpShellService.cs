namespace Briefcase.Services.Shell;

// Used when the host OS isn't Windows, macOS, or Linux. The web UI hides the Open / Show in
// folder buttons when IsSupported is false.
public class NoOpShellService : IShellService
{
    public bool IsSupported => false;

    public void Open(string absolutePath) =>
        throw new PlatformNotSupportedException("Opening files is not supported on this operating system.");

    public void ShowInFolder(string absolutePath) =>
        throw new PlatformNotSupportedException("Showing files in a folder is not supported on this operating system.");
}
