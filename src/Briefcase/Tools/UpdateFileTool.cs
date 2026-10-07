using System.ComponentModel;
using System.Text.Json;
using Briefcase.Services;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class UpdateFileTool
{
    private readonly FileContentService fileContentService;

    public UpdateFileTool(FileContentService fileContentService)
    {
        this.fileContentService = fileContentService;
    }

    [McpServerTool(Name = "update_file")]
    [Description(
        "Replaces the full content of an existing Briefcase file. The file is identified by the ID returned from list_files or create_file. " +
        "To update a binary file (e.g. an image or PDF), pass its bytes base64-encoded with encoding 'base64'; " +
        "text content is refused for binary file types.")]
    public string UpdateFile(
        [Description("The file ID (GUID) returned by list_files or create_file.")] Guid id,
        [Description("The new full content of the file: plain text, or base64-encoded bytes when encoding is 'base64'. The entire existing content is replaced.")] string content,
        [Description("How 'content' is encoded: 'text' (default) or 'base64' for binary files.")] string? encoding = null)
    {
        if (!FileContentService.TryParseEncoding(encoding, out var contentEncoding))
            return JsonSerializer.Serialize(new { error = $"Unknown encoding '{encoding}'. Use 'text' or 'base64'." });

        var result = fileContentService.UpdateFile(id, content, encoding: contentEncoding);

        if (!result.Success)
            return JsonSerializer.Serialize(new { error = result.Error });

        return JsonSerializer.Serialize(
            new
            {
                id = result.Id,
                name = result.Name,
                size = result.Size,
                lastModified = result.LastModifiedUtc
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
