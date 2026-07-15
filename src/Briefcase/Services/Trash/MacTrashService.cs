namespace Briefcase.Services.Trash;

public class MacTrashService : ITrashService
{
    public void Trash(string absolutePath)
    {
        var trashDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "..", ".Trash");
        trashDir = Path.GetFullPath(trashDir);
        Directory.CreateDirectory(trashDir);

        var destination = Path.Combine(trashDir, Path.GetFileName(absolutePath));
        destination = DeduplicatePath(destination);

        File.Move(absolutePath, destination);
    }

    private static string DeduplicatePath(string destination)
    {
        if (!File.Exists(destination))
            return destination;

        var directory = Path.GetDirectoryName(destination)!;
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);

        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{nameWithoutExtension} {counter}{extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
