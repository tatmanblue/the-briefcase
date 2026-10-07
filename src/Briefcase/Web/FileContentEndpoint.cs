using Briefcase.Registry;
using Briefcase.Services.Content;

namespace Briefcase.Web;

// Streams a file's raw bytes to the browser by GUID so the viewer can show images, PDFs, audio and
// video, and offer a download. The file is looked up through the registry only -- no path ever
// appears in the URL or response.
public static class FileContentEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/files/{id:guid}/content", (Guid id, bool? download, HttpContext context,
            FileRegistry registry, FileTypeClassifier classifier) =>
        {
            var entry = registry.GetById(id);
            if (entry is null || !File.Exists(entry.AbsolutePath))
                return Results.NotFound();

            var fileType = classifier.Classify(entry.Name, entry.AbsolutePath);

            // nosniff: the browser must use our MIME type, never guess one. sandbox: if this URL is
            // opened directly (rather than through <img>), an SVG or HTML file must not be able to
            // run script on the web UI's origin. PDFs are exempt -- Chrome refuses to render a PDF
            // in a sandboxed document, and its viewer doesn't run the PDF's own script anyway.
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (fileType.Kind != FileKind.Pdf)
                context.Response.Headers.ContentSecurityPolicy = "sandbox";

            return Results.File(
                entry.AbsolutePath,
                fileType.MimeType,
                fileDownloadName: download == true ? entry.Name : null,
                enableRangeProcessing: true);
        });
    }
}
