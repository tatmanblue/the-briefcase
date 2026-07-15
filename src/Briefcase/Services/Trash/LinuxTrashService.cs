using System.Globalization;
using System.Text;

namespace Briefcase.Services.Trash;

// Implements the freedesktop.org Trash spec (files + .trashinfo) so deleted files show up
// correctly in GNOME Files, Dolphin, etc. https://specifications.freedesktop.org/trash-spec/trashspec-latest.html
public class LinuxTrashService : ITrashService
{
    public void Trash(string absolutePath)
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        var trashFilesDir = Path.Combine(dataHome, "Trash", "files");
        var trashInfoDir = Path.Combine(dataHome, "Trash", "info");
        Directory.CreateDirectory(trashFilesDir);
        Directory.CreateDirectory(trashInfoDir);

        var fileName = Path.GetFileName(absolutePath);
        var (destination, infoPath) = DeduplicatePaths(trashFilesDir, trashInfoDir, fileName);

        var trashInfo = new StringBuilder()
            .AppendLine("[Trash Info]")
            .AppendLine($"Path={Uri.EscapeDataString(absolutePath)}")
            .AppendLine($"DeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}")
            .ToString();

        File.WriteAllText(infoPath, trashInfo);
        File.Move(absolutePath, destination);
    }

    private static (string destination, string infoPath) DeduplicatePaths(string trashFilesDir, string trashInfoDir, string fileName)
    {
        var destination = Path.Combine(trashFilesDir, fileName);
        var infoPath = Path.Combine(trashInfoDir, fileName + ".trashinfo");

        if (!File.Exists(destination) && !File.Exists(infoPath))
            return (destination, infoPath);

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        var counter = 1;
        do
        {
            var candidateName = $"{nameWithoutExtension} {counter}{extension}";
            destination = Path.Combine(trashFilesDir, candidateName);
            infoPath = Path.Combine(trashInfoDir, candidateName + ".trashinfo");
            counter++;
        } while (File.Exists(destination) || File.Exists(infoPath));

        return (destination, infoPath);
    }
}
