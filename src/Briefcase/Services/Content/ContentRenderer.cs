using System.Net;
using Markdig;

namespace Briefcase.Services.Content;

public class ContentRenderer
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // Renders file content to HTML based on file extension. Markdown (.md) is rendered to
    // formatted HTML; everything else is shown as HTML-encoded plain text.
    public string RenderToHtml(string fileName, string content)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            ? Markdown.ToHtml(content, MarkdownPipeline)
            : $"<pre>{WebUtility.HtmlEncode(content)}</pre>";
    }
}
