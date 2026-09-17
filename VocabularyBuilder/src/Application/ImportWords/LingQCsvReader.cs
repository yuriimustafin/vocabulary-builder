using System.Text;

namespace VocabularyBuilder.Application.ImportWords;

/// <summary>
/// One row of a LingQ vocabulary export.
/// </summary>
/// <param name="Term">The term as saved in LingQ, usually with its article: "une conférence".</param>
/// <param name="Phrase">The sentence it was met in, where LingQ recorded one.</param>
public record LingQRow(string Term, string? Phrase);

/// <summary>
/// Reads a LingQ vocabulary export.
/// </summary>
/// <remarks>
/// The export is
/// <c>term,phrase,tag1..tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2</c>.
/// Only the first two columns are read: the meanings are the learner's own translations,
/// and imported words take their content from the dictionary instead. The tags are left
/// alone too - they carry an infinitive often enough to be tempting and not often enough
/// to be trusted.
///
/// Hand-rolled rather than taken from a package because the format is small and the
/// project has no CSV dependency; it handles the one complication the export actually
/// contains, which is quoted fields holding commas.
/// </remarks>
public static class LingQCsvReader
{
    public static IReadOnlyList<LingQRow> Read(string csvContent)
    {
        var rows = new List<LingQRow>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return rows;
        }

        var records = ParseRecords(csvContent);

        if (records.Count == 0)
        {
            return rows;
        }

        // The export carries a header, but a file that has had it stripped should still
        // import rather than silently lose its first word
        var start = IsHeader(records[0]) ? 1 : 0;

        for (var i = start; i < records.Count; i++)
        {
            var fields = records[i];

            if (fields.Count == 0)
            {
                continue;
            }

            var term = fields[0].Trim();

            if (term.Length == 0)
            {
                continue;
            }

            var phrase = fields.Count > 1 ? fields[1].Trim() : null;

            rows.Add(new LingQRow(term, string.IsNullOrEmpty(phrase) ? null : phrase));
        }

        return rows;
    }

    private static bool IsHeader(IReadOnlyList<string> fields)
    {
        return fields.Count > 0 && fields[0].Trim().Equals("term", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits the file into records of fields, honouring quoted fields - which may contain
    /// commas, newlines, and doubled quotes standing for one.
    /// </summary>
    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                    continue;
                }

                if (i + 1 < content.Length && content[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;

                case '\r':
                    break;

                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(fields);
                    fields = new List<string>();
                    break;

                default:
                    field.Append(c);
                    break;
            }
        }

        // Whatever the file ends with, a last record without a trailing newline is still a record
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(fields);
        }

        return records
            .Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();
    }
}
