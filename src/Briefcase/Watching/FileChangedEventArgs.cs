namespace Briefcase.Watching;

public class FileChangedEventArgs : EventArgs
{
    public required string AbsolutePath { get; init; }
    public required WatcherChangeTypes ChangeType { get; init; }
    public string? OldPath { get; init; }
}
