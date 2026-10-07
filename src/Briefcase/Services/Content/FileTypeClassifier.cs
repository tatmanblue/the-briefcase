using Microsoft.AspNetCore.StaticFiles;

namespace Briefcase.Services.Content;

public enum FileKind { Markdown, Text, Image, Pdf, Audio, Video, Binary }

// IsTextBased = the file's bytes are human-readable text, so it can be read as a string, edited in
// a textarea, and written with encoding "text". SVG is the notable case where Kind (Image) and
// IsTextBased (true) are both meaningful.
public record FileTypeInfo(FileKind Kind, string MimeType, bool IsTextBased)
{
    public string KindName => Kind.ToString().ToLowerInvariant();
}

// Single source of truth for "what kind of file is this", shared by the web viewer and the MCP
// tools so both always agree. Classification is extension-based; files with an extension we don't
// recognise are sniffed (first 8 KB, NUL byte = binary) so plain-text files with unusual or missing
// extensions (.ini, Dockerfile, ...) are still treated as text.
public class FileTypeClassifier
{
    private const string FALLBACK_MIME_TYPE = "application/octet-stream";
    private const int SNIFF_BYTES = 8192;

    // Extensions the built-in provider either doesn't know or maps to a non-text/* type, but which
    // are plain text in practice.
    private static readonly Dictionary<string, string> TextMimeOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".csv"] = "text/csv",
        [".tsv"] = "text/tab-separated-values",
        [".json"] = "application/json",
        [".jsonc"] = "application/json",
        [".xml"] = "application/xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".toml"] = "application/toml",
        [".ini"] = "text/plain",
        [".cfg"] = "text/plain",
        [".conf"] = "text/plain",
        [".env"] = "text/plain",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".ts"] = "text/plain",
        [".tsx"] = "text/plain",
        [".jsx"] = "text/plain",
        [".css"] = "text/css",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".cs"] = "text/plain",
        [".csproj"] = "application/xml",
        [".razor"] = "text/plain",
        [".py"] = "text/plain",
        [".rb"] = "text/plain",
        [".go"] = "text/plain",
        [".rs"] = "text/plain",
        [".java"] = "text/plain",
        [".kt"] = "text/plain",
        [".swift"] = "text/plain",
        [".c"] = "text/plain",
        [".h"] = "text/plain",
        [".cpp"] = "text/plain",
        [".hpp"] = "text/plain",
        [".sql"] = "text/plain",
        [".sh"] = "text/plain",
        [".ps1"] = "text/plain",
        [".bat"] = "text/plain",
        [".cmd"] = "text/plain",
        [".svg"] = "image/svg+xml"
    };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };

    private readonly FileExtensionContentTypeProvider contentTypeProvider = new();

    // absolutePath is optional: when supplied and the extension is unrecognised, the file's first
    // bytes are sniffed. Without it (e.g. classifying a not-yet-written file name) unknown
    // extensions are reported as binary/octet-stream.
    public FileTypeInfo Classify(string fileName, string? absolutePath = null)
    {
        var extension = Path.GetExtension(fileName);

        if (TextMimeOverrides.TryGetValue(extension, out var overrideMime))
            return FromMime(extension, overrideMime);

        if (contentTypeProvider.TryGetContentType(fileName, out var mime))
            return FromMime(extension, mime);

        if (absolutePath != null && LooksLikeText(absolutePath))
            return new FileTypeInfo(FileKind.Text, "text/plain", true);

        return new FileTypeInfo(FileKind.Binary, FALLBACK_MIME_TYPE, false);
    }

    // Whether writing text content to this file makes sense. Text into an image/PDF/archive would
    // silently corrupt it. A new file (no absolutePath) with an extension we don't recognise is
    // allowed -- nothing says it's binary, and refusing would break e.g. 'Dockerfile' or 'notes.foo'.
    public bool AcceptsTextContent(string fileName, string? absolutePath = null)
    {
        var fileType = Classify(fileName, absolutePath);
        if (fileType.IsTextBased)
            return true;

        return absolutePath == null && !IsRecognised(fileName);
    }

    private bool IsRecognised(string fileName) =>
        TextMimeOverrides.ContainsKey(Path.GetExtension(fileName))
        || contentTypeProvider.TryGetContentType(fileName, out _);

    private static FileTypeInfo FromMime(string extension, string mime)
    {
        if (MarkdownExtensions.Contains(extension))
            return new FileTypeInfo(FileKind.Markdown, mime, true);
        if (mime == "image/svg+xml")
            return new FileTypeInfo(FileKind.Image, mime, true);
        if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return new FileTypeInfo(FileKind.Image, mime, false);
        if (mime == "application/pdf")
            return new FileTypeInfo(FileKind.Pdf, mime, false);
        if (mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return new FileTypeInfo(FileKind.Audio, mime, false);
        if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return new FileTypeInfo(FileKind.Video, mime, false);
        if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || TextMimeOverrides.ContainsKey(extension))
            return new FileTypeInfo(FileKind.Text, mime, true);

        return new FileTypeInfo(FileKind.Binary, mime, false);
    }

    private static bool LooksLikeText(string absolutePath)
    {
        try
        {
            using var stream = File.OpenRead(absolutePath);
            var buffer = new byte[SNIFF_BYTES];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Array.IndexOf(buffer, (byte)0, 0, read) < 0;
        }
        catch
        {
            return false;
        }
    }
}
