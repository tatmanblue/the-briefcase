using System.Text;

namespace Briefcase.Services.Content;

// Splits CSV/TSV text into rows of fields for the web viewer's table view. Follows RFC 4180: a
// field that starts with a quote may contain the delimiter, line breaks and "" (an escaped quote).
// Lenient by design -- malformed input never throws; stray characters are kept as field text.
public static class DelimitedTextParser
{
    public static IEnumerable<List<string>> Parse(string text, char delimiter)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var atFieldStart = true;
        var rowHasContent = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            rowHasContent = true;

            if (inQuotes)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                    field.Append(text[++i]);
                else
                    inQuotes = false;
                continue;
            }

            if (c == '"' && atFieldStart)
            {
                inQuotes = true;
                atFieldStart = false;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(field.ToString());
                field.Clear();
                atFieldStart = true;
                rowHasContent = false;

                // Blank lines (commonly trailing ones) aren't data rows.
                if (row is not [""])
                    yield return row;
                row = new List<string>();
            }
            else
            {
                field.Append(c);
                atFieldStart = false;
            }
        }

        if (rowHasContent)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }
}
