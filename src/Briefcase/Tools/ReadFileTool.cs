using System.ComponentModel;
using System.Text.Json;
using Briefcase.Registry;
using Briefcase.Services.Content;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class ReadFileTool
{
    // Raster formats multimodal clients broadly accept as image content. Other image types (bmp,
    // tiff, ico, ...) are often rejected by the model, so they're reported as metadata only.
    private static readonly HashSet<string> InlineImageMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/gif", "image/webp" };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly FileRegistry registry;
    private readonly FileTypeClassifier classifier;

    public ReadFileTool(FileRegistry registry, FileTypeClassifier classifier)
    {
        this.registry = registry;
        this.classifier = classifier;
    }

    [McpServerTool(Name = "read_file")]
    [Description(
        "Reads the content of a file by its ID. Returns metadata (name, size, last modified, mimeType, kind) along with the content. " +
        "Text files (kind 'markdown' or 'text', and SVG images) return their text in 'content'. " +
        "PNG, JPEG, GIF and WebP images are returned as image content alongside the metadata; audio files as audio content. " +
        "Other binary files (PDFs, archives, ...) return metadata only, with contentOmitted set to true. " +
        "Use list_files to discover available file IDs.")]
    public CallToolResult ReadFile(
        [Description("The file ID (GUID) returned by list_files.")] Guid id)
    {
        var entry = registry.GetById(id);
        if (entry is null)
            return Json(new { error = $"No file found with ID '{id}'." });

        if (!File.Exists(entry.AbsolutePath))
            return Json(new { error = $"File '{entry.Name}' is registered but no longer exists on disk." });

        var info = new FileInfo(entry.AbsolutePath);
        var fileType = classifier.Classify(entry.Name, entry.AbsolutePath);

        try
        {
            if (fileType.IsTextBased)
            {
                var content = File.ReadAllText(entry.AbsolutePath);
                return Json(new
                {
                    id = entry.Id,
                    name = entry.Name,
                    size = info.Length,
                    lastModified = info.LastWriteTimeUtc,
                    mimeType = fileType.MimeType,
                    kind = fileType.KindName,
                    content
                });
            }

            ContentBlock? binaryBlock = null;
            if (fileType.Kind == FileKind.Image && InlineImageMimeTypes.Contains(fileType.MimeType))
                binaryBlock = ImageContentBlock.FromBytes(File.ReadAllBytes(entry.AbsolutePath), fileType.MimeType);
            else if (fileType.Kind == FileKind.Audio)
                binaryBlock = AudioContentBlock.FromBytes(File.ReadAllBytes(entry.AbsolutePath), fileType.MimeType);

            var metadata = new
            {
                id = entry.Id,
                name = entry.Name,
                size = info.Length,
                lastModified = info.LastWriteTimeUtc,
                mimeType = fileType.MimeType,
                kind = fileType.KindName,
                contentOmitted = binaryBlock is null,
                note = binaryBlock is null
                    ? "Binary file: content is not returned as text."
                    : $"File content is attached as {fileType.KindName} content."
            };

            var result = Json(metadata);
            if (binaryBlock != null)
                result.Content.Add(binaryBlock);
            return result;
        }
        catch (Exception ex)
        {
            return Json(new { error = $"Failed to read file '{entry.Name}': {ex.Message}" });
        }
    }

    private static CallToolResult Json(object value) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, JsonOptions) }]
    };
}
