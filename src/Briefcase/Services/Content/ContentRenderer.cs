using System.Net;
using System.Text;
using Briefcase.Registry;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Briefcase.Services.Content;

public class ContentRenderer
{
    // highlight.js runs in the browser and stalls on very large files; above this, code is shown
    // as plain text.
    private const int HIGHLIGHT_MAX_CHARS = 1024 * 1024;
    // CSV/TSV tables stop here. "Show raw source" and Download still give the whole file.
    private const int TABLE_MAX_ROWS = 1000;

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // File extension -> highlight.js language name. Only languages in the vendored bundle
    // (wwwroot/lib/highlightjs) are listed.
    private static readonly Dictionary<string, string> HighlightLanguagesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".csx"] = "csharp",
        [".vb"] = "vbnet",
        [".js"] = "javascript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".jsx"] = "javascript",
        [".ts"] = "typescript",
        [".tsx"] = "typescript",
        [".json"] = "json",
        [".jsonc"] = "json",
        [".xml"] = "xml",
        [".xaml"] = "xml",
        [".csproj"] = "xml",
        [".props"] = "xml",
        [".targets"] = "xml",
        [".html"] = "xml",
        [".htm"] = "xml",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "ini",
        [".ini"] = "ini",
        [".cfg"] = "ini",
        [".conf"] = "ini",
        [".css"] = "css",
        [".scss"] = "scss",
        [".less"] = "less",
        [".py"] = "python",
        [".rb"] = "ruby",
        [".go"] = "go",
        [".rs"] = "rust",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",
        [".swift"] = "swift",
        [".c"] = "c",
        [".h"] = "c",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".m"] = "objectivec",
        [".sql"] = "sql",
        [".sh"] = "bash",
        [".bash"] = "bash",
        [".zsh"] = "bash",
        [".ps1"] = "powershell",
        [".psm1"] = "powershell",
        [".psd1"] = "powershell",
        [".bat"] = "dos",
        [".cmd"] = "dos",
        [".lua"] = "lua",
        [".pl"] = "perl",
        [".php"] = "php",
        [".r"] = "r",
        [".graphql"] = "graphql",
        [".gql"] = "graphql",
        [".diff"] = "diff",
        [".patch"] = "diff",
        [".mk"] = "makefile"
    };

    private static readonly Dictionary<string, string> HighlightLanguagesByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "dockerfile",
        ["Makefile"] = "makefile",
        ["GNUmakefile"] = "makefile"
    };

    private readonly FileRegistry registry;

    public ContentRenderer(FileRegistry registry)
    {
        this.registry = registry;
    }

    // Renders a text-based file to HTML for the web viewer. Markdown is rendered (with relative
    // links to other registered files pointed at the viewer); CSV/TSV become a table; code is
    // wrapped for highlight.js; anything else is shown as HTML-encoded plain text.
    public string RenderToHtml(string absolutePath, FileTypeInfo fileType, string content)
    {
        if (fileType.Kind == FileKind.Markdown)
            return RenderMarkdown(absolutePath, content);

        var extension = Path.GetExtension(absolutePath);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            return RenderTable(content, ',');
        if (string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase))
            return RenderTable(content, '\t');

        return RenderCode(absolutePath, content);
    }

    private string RenderMarkdown(string absolutePath, string content)
    {
        var document = Markdown.Parse(content, MarkdownPipeline);
        var baseDirectory = Path.GetDirectoryName(absolutePath) ?? string.Empty;

        // Relative links such as ![](diagram.png) or [spec](../spec.md) mean nothing to the browser,
        // which is looking at /files/{id}. Point any that resolve to a registered file at the
        // content endpoint (images) or that file's viewer page (links). Only registered files are
        // rewritten, so nothing outside the configured paths is ever reachable this way.
        foreach (var link in document.Descendants<LinkInline>())
        {
            var target = ResolveRegisteredFile(baseDirectory, link.Url, out var fragment);
            if (target is null)
                continue;

            link.Url = link.IsImage
                ? $"/files/{target.Id}/content"
                : $"/files/{target.Id}{fragment}";
        }

        return document.ToHtml(MarkdownPipeline);
    }

    private RegistryEntry? ResolveRegisteredFile(string baseDirectory, string? url, out string fragment)
    {
        fragment = string.Empty;

        // Skip in-page anchors, root-relative and absolute URLs (http:, mailto:, C:\..., file:).
        if (string.IsNullOrWhiteSpace(url) || url[0] is '#' or '/' or '\\'
            || Uri.TryCreate(url, UriKind.Absolute, out _))
            return null;

        var path = url;
        var hashIndex = path.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = path[hashIndex..];
            path = path[..hashIndex];
        }

        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        if (path.Length == 0)
            return null;

        try
        {
            return registry.GetByPath(Path.Combine(baseDirectory, Uri.UnescapeDataString(path)));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static string RenderTable(string content, char delimiter)
    {
        List<string>? header = null;
        var rows = new List<List<string>>();
        var totalRows = 0;

        foreach (var row in DelimitedTextParser.Parse(content, delimiter))
        {
            if (header is null)
            {
                header = row;
                continue;
            }

            totalRows++;
            if (rows.Count < TABLE_MAX_ROWS)
                rows.Add(row);
        }

        if (header is null)
            return "<pre></pre>";

        var columnCount = Math.Max(header.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        var html = new StringBuilder();

        if (totalRows > rows.Count)
            html.Append($"<p class=\"table-note\">Showing the first {rows.Count:N0} of {totalRows:N0} rows. Use \"Show raw source\" or Download for the rest.</p>");

        html.Append("<div class=\"table-scroll\"><table class=\"data-table\"><thead><tr>");
        AppendCells(html, header, columnCount, "th");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            AppendCells(html, row, columnCount, "td");
            html.Append("</tr>");
        }
        html.Append("</tbody></table></div>");

        return html.ToString();
    }

    // Ragged rows are padded so every row has the same number of cells.
    private static void AppendCells(StringBuilder html, List<string> cells, int columnCount, string tag)
    {
        for (var i = 0; i < columnCount; i++)
            html.Append($"<{tag}>{WebUtility.HtmlEncode(i < cells.Count ? cells[i] : string.Empty)}</{tag}>");
    }

    private static string RenderCode(string absolutePath, string content)
    {
        var encoded = WebUtility.HtmlEncode(content);
        var language = content.Length <= HIGHLIGHT_MAX_CHARS ? HighlightLanguageFor(absolutePath) : null;

        return language is null
            ? $"<pre>{encoded}</pre>"
            : $"<pre><code class=\"language-{language}\">{encoded}</code></pre>";
    }

    private static string? HighlightLanguageFor(string absolutePath)
    {
        if (HighlightLanguagesByFileName.TryGetValue(Path.GetFileName(absolutePath), out var byName))
            return byName;

        return HighlightLanguagesByExtension.TryGetValue(Path.GetExtension(absolutePath), out var byExtension)
            ? byExtension
            : null;
    }
}
