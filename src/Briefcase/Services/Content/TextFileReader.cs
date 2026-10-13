namespace Briefcase.Services.Content;

// Reads a text file for the web viewer, stopping at a size cap so a huge log or data dump is never
// pulled into memory -- and down the Blazor circuit -- in full.
public static class TextFileReader
{
    public static (string Text, bool Truncated) Read(string absolutePath, long? maxBytes)
    {
        if (maxBytes is null || new FileInfo(absolutePath).Length <= maxBytes)
            return (File.ReadAllText(absolutePath), false);

        // StreamReader honours a byte-order mark (UTF-16, ...) just as File.ReadAllText does. The
        // cap is applied in characters, which is close enough to bytes for a display preview.
        using var reader = new StreamReader(absolutePath, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[(int)Math.Min(maxBytes.Value, Array.MaxLength)];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return (new string(buffer, 0, read), true);
    }
}
