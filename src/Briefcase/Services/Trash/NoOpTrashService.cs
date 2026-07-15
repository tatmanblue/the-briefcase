using Microsoft.Extensions.Logging;

namespace Briefcase.Services.Trash;

// Used when the host OS isn't Windows, macOS, or Linux. Delete is effectively disabled rather
// than risking a permanent, unrecoverable File.Delete on a platform we haven't implemented trash for.
public class NoOpTrashService : ITrashService
{
    private readonly ILogger<NoOpTrashService> logger;

    public NoOpTrashService(ILogger<NoOpTrashService> logger)
    {
        this.logger = logger;
    }

    public void Trash(string absolutePath)
    {
        logger.LogWarning(
            "Delete is not supported on this operating system. File was not moved to trash: {Path}",
            absolutePath);
    }
}
