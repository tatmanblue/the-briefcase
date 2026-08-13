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
    [Description("Replaces the full content of an existing Briefcase file. The file is identified by the ID returned from list_files or create_file.")]
    public string UpdateFile(
        [Description("The file ID (GUID) returned by list_files or create_file.")] Guid id,
        [Description("The new full content of the file. The entire existing content is replaced.")] string content)
    {
        var result = fileContentService.UpdateFile(id, content);

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
