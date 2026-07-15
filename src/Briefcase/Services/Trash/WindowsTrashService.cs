using Microsoft.VisualBasic.FileIO;

namespace Briefcase.Services.Trash;

public class WindowsTrashService : ITrashService
{
    public void Trash(string absolutePath)
    {
        FileSystem.DeleteFile(absolutePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }
}
