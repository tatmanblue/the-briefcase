namespace Briefcase.Services.Shell;

// Hands a file to the host OS desktop: open it with its associated application, or reveal it in
// the system file manager. Runs on the server machine -- which is the user's own desktop, since
// the web UI only binds to 127.0.0.1. Implementations throw on failure.
public interface IShellService
{
    bool IsSupported { get; }
    void Open(string absolutePath);
    void ShowInFolder(string absolutePath);
}
