using System.ComponentModel;
using System.Text.Json;
using Briefcase.Services;
using ModelContextProtocol.Server;

namespace Briefcase.Tools;

internal class CreateFileTool
{
    private readonly FileContentService fileContentService;

    public CreateFileTool(FileContentService fileContentService)
    {
        this.fileContentService = fileContentService;
    }

    [McpServerTool(Name = "create_file")]
    [Description(
        "Creates a new file in the Briefcase and makes it immediately available to all agents. " +
        "Returns the new file's ID, which can be passed to read_file or update_file. " +
        "Optionally associates the file with a project by providing a projectId.")]
    public string CreateFile(
        [Description("The file name, including extension (e.g. 'notes.txt'). Path separators are stripped.")] string name,
        [Description("The full content of the new file.")] string content,
        [Description("Optional project ID (GUID) to associate the new file with. The call fails if the ID does not exist.")] Guid? projectId = null)
    {
        var result = fileContentService.CreateFile(name, content, projectId);

        if (!result.Success)
            return JsonSerializer.Serialize(new { error = result.Error });

        return JsonSerializer.Serialize(
            new
            {
                id = result.Id,
                name = result.Name,
                size = result.Size,
                lastModified = result.LastModifiedUtc,
                projectId = result.ProjectId
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
