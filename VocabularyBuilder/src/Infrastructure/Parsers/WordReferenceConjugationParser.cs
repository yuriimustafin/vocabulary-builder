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
/// Page shape: table#conjtable lists the infinitive and both participles as label/value
/// rows. Below it an h4 names each mood ("indicatif", "subjonctif", "formes composées /
/// compound tenses"), and each table.neoConj under it opens with a th naming the tense,
/// then one row per person: th = subject, td = form. Stems are marked up with &lt;b&gt;,
/// so a form has to be read as text rather than taken from a single node.
///
/// Every cell is kept, including a form that recurs ("je prends", "tu prends"), so the
/// forms can be shown as the table they came from.
/// </remarks>
public class WordReferenceConjugationParser : IConjugationParser
{
    private const string ParticipleMood = "participe";

    /// <summary>The imperative has no first or third person; those cells hold a dash.</summary>
    private static readonly HashSet<string> EmptyCells = new() { "-", "–", "—" };

    private readonly HtmlParser _htmlParser = new();

    public async Task<IReadOnlyList<WordForm>> GetFormsAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<WordForm>();
        }

        var document = await _htmlParser.ParseDocumentAsync(html);
        var forms = new List<WordForm>();
        var seen = new HashSet<(string?, string?, string?, string)>();

        void Add(string? mood, string? tense, string? person, string form)
        {
            // Guards only against the same cell being read twice, not against a form that
            // legitimately appears in several cells
            if (seen.Add((mood, tense, person, form)))
            {
                forms.Add(new WordForm
                {
                    Form = form,
                    Language = Language.French,
                    Mood = mood,
                    Tense = tense,
                    Person = person
                });
            }
        }

        foreach (var (tense, form) in ReadParticiples(document))
        {
            Add(ParticipleMood, tense, null, form);
        }

        foreach (var table in document.QuerySelectorAll("table.neoConj"))
        {
            var mood = ReadMood(table);
            var tense = ReadTense(table);

            foreach (var row in table.QuerySelectorAll("tr"))
            {
                var personCell = row.QuerySelector("th[scope='row']");
                var cell = row.QuerySelector("td");

                if (personCell == null || cell == null)
                {
                    continue;
                }

                var person = CleanPerson(personCell.TextContent);

                foreach (var form in SplitForms(cell.TextContent))
                {
                    Add(mood, tense, person, form);
                }
            }
        }

        return forms;
    }

    /// <summary>
    /// "participe présent : prenant" and "participe passé : pris". The infinitive is the
    /// headword itself, and the pronominal form is a separate verb, so neither is kept.
    /// </summary>
    private static IEnumerable<(string Tense, string Form)> ReadParticiples(IDocument document)
    {
        foreach (var row in document.QuerySelectorAll("table#conjtable tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length < 2)
            {
                continue;
            }

            var label = Collapse(cells[0].TextContent).TrimEnd(':', ' ');
            if (!label.StartsWith(ParticipleMood + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tense = label[(ParticipleMood.Length + 1)..].Trim();

            foreach (var form in SplitForms(cells[1].TextContent))
            {
                yield return (tense, form);
            }
        }
    }

    /// <summary>
    /// The tense sits in the table's own column header.
    /// </summary>
    private static string? ReadTense(IElement table)
    {
        var header = Collapse(table.QuerySelector("th[scope='col']")?.TextContent);
        return header.Length == 0 ? null : header;
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
                return Collapse(node.TextContent);
            }

            node = node.PreviousElementSibling;
        }

        // Tables are grouped in a div per mood, so try the container's heading
        var heading = table.ParentElement?.QuerySelector("h4")?.TextContent;
        return heading == null ? null : Collapse(heading);
    }

    /// <summary>
    /// "je", "que tu", "il, elle, on" are kept as written. The imperative puts its person
    /// in parentheses - "(tu)" - which is dropped, and an empty person becomes null.
    /// </summary>
    private static string? CleanPerson(string text)
    {
        var person = Collapse(text).Trim('(', ')', ' ');
        return person.Length == 0 ? null : person;
    }

    /// <summary>
    /// Compound tenses render as "ai pris", which is the whole form, but a cell may also
    /// hold alternatives separated by commas. The imperative adds " !", which is not part
    /// of the form, and marks the persons it lacks with a dash.
    /// </summary>
    private static IEnumerable<string> SplitForms(string cellText)
    {
        var text = Collapse(cellText);

        if (text.Length == 0 || EmptyCells.Contains(text))
        {
            yield break;
        }

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var form = part.Trim().TrimEnd('!').Trim();
            if (form.Length > 0 && !EmptyCells.Contains(form))
            {
                yield return form;
            }
        }
    }

    private static string Collapse(string? text) =>
        string.Join(' ', (text ?? string.Empty)
            .Replace(' ', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
