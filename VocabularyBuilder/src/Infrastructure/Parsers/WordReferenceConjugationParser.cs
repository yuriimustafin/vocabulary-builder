using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Reads WordReference conjugation tables
/// (https://www.wordreference.com/conj/frverbs.aspx?v=prendre) into the
/// inflected forms of a verb.
/// </summary>
/// <remarks>
/// Page shape: an h4 names the mood ("indicatif", "subjonctif", "formes
/// composées / compound tenses"), and each table.neoConj under it opens with a
/// th naming the tense, then one row per person: th = subject, td = form.
/// Stems are marked up with &lt;b&gt;, so the form has to be read as text
/// rather than taken from a single node.
/// </remarks>
public class WordReferenceConjugationParser : IConjugationParser
{
    private readonly HtmlParser _htmlParser = new();

    public async Task<IReadOnlyList<WordForm>> GetFormsAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<WordForm>();
        }

        var document = await _htmlParser.ParseDocumentAsync(html);
        var forms = new List<WordForm>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var table in document.QuerySelectorAll("table.neoConj"))
        {
            var mood = ReadMood(table);
            var tense = ReadTense(table);

            foreach (var row in table.QuerySelectorAll("tr"))
            {
                var person = row.QuerySelector("th[scope='row']")?.TextContent?.Trim();
                var cell = row.QuerySelector("td");

                if (person == null || cell == null)
                {
                    continue;
                }

                foreach (var form in SplitForms(cell.TextContent))
                {
                    // The same form recurs across tenses; one row per form is enough
                    if (!seen.Add(form))
                    {
                        continue;
                    }

                    forms.Add(new WordForm
                    {
                        Form = form,
                        Language = Language.French,
                        Mood = mood,
                        Tense = tense,
                        Person = string.IsNullOrWhiteSpace(person) ? null : person
                    });
                }
            }
        }

        return forms;
    }

    /// <summary>
    /// The tense sits in the table's own column header.
    /// </summary>
    private static string? ReadTense(IElement table)
    {
        var header = table.QuerySelector("th[scope='col']")?.TextContent?.Trim();
        return string.IsNullOrWhiteSpace(header) ? null : header;
    }

    /// <summary>
    /// The mood is the nearest h4 before the table.
    /// </summary>
    private static string? ReadMood(IElement table)
    {
        var node = table.PreviousElementSibling;

        while (node != null)
        {
            if (string.Equals(node.TagName, "H4", StringComparison.OrdinalIgnoreCase))
            {
                return node.TextContent?.Trim();
            }

            node = node.PreviousElementSibling;
        }

        // Tables are grouped in a div per mood, so try the container's heading
        return table.ParentElement?.QuerySelector("h4")?.TextContent?.Trim();
    }

    /// <summary>
    /// Compound tenses render as "ai pris", which is the whole form, but a cell
    /// may also hold alternatives. Keep the cell text as one form and trim it.
    /// </summary>
    private static IEnumerable<string> SplitForms(string cellText)
    {
        var text = cellText?.Replace('\u00a0', ' ').Trim();

        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            yield break;
        }

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var form = string.Join(' ', part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrWhiteSpace(form))
            {
                yield return form;
            }
        }
    }
}
