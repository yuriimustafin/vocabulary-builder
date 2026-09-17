using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Parses French words from the WordReference French-English dictionary,
/// replacing the GPT-generated definitions that French previously relied on.
/// </summary>
/// <remarks>
/// Page shape (https://www.wordreference.com/fren/prendre):
/// three table.WRD blocks, each opening with its own section header row -
/// Principal Translations (td#regular), Additional Translations (td#additional)
/// and Compound Forms (td#compounds). Only the first is read.
///
/// Within that table a row carrying id="fren:..." opens an entry; the rows that
/// follow it without an id belong to the same entry and carry extra
/// translations (td.ToWrd) and examples (td.FrEx / td.ToEx).
/// </remarks>
public class WordReferenceFrenchParser : IWordReferenceParser
{
    private const string DictionaryUrl = "https://www.wordreference.com/fren/";
    private const string BaseUrl = "https://www.wordreference.com";

    private readonly WordReferenceOptions _options;
    private readonly IWordReferencePageLoader _pageLoader;
    private readonly IConjugationParser _conjugationParser;
    private readonly HtmlParser _htmlParser = new();

    public DictionarySourceType SourceType => DictionarySourceType.WordReference;

    public WordReferenceFrenchParser(
        IOptions<WordReferenceOptions> options,
        IWordReferencePageLoader pageLoader,
        IConjugationParser conjugationParser)
    {
        _options = options.Value;
        _pageLoader = pageLoader;
        _conjugationParser = conjugationParser;
    }

    public async Task<IEnumerable<Word>> GetWords(IEnumerable<string> searchedWords)
    {
        var results = await GetWordsWithSource(searchedWords);
        return results.Select(r => r.Word);
    }

    public async Task<IEnumerable<WordParseResult>> GetWordsWithSource(IEnumerable<string> searchedWords)
    {
        var results = new List<WordParseResult>();
        var first = true;

        foreach (var searchedWord in searchedWords)
        {
            var term = Normalise(searchedWord);
            if (string.IsNullOrEmpty(term))
            {
                continue;
            }

            // Space the requests out, but do not pay the delay before the first
            if (!first)
            {
                await Task.Delay(_options.RequestDelayMilliseconds);
            }
            first = false;

            try
            {
                var url = GetAddress(term);
                var html = await _pageLoader.GetPageAsync(url);

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine($"No response from WordReference for word: {term}");
                    continue;
                }

                var document = await _htmlParser.ParseDocumentAsync(html);
                var word = GetWord(document, term);

                if (word == null)
                {
                    Console.WriteLine($"WordReference has no entry for: {term}");
                    continue;
                }

                var result = new WordParseResult
                {
                    Word = word,
                    SearchedTerm = term,
                    SourceHtml = html,
                    SourceUrl = url
                };

                await AddConjugation(document, result);

                results.Add(result);
                Console.WriteLine($"Successfully parsed French word: {word.Headword}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing French word '{term}': {ex.Message}");
            }
        }

        return results;
    }

    public async Task<Word?> GetWordFromCachedHtml(string cachedHtml)
    {
        if (string.IsNullOrWhiteSpace(cachedHtml))
        {
            return null;
        }

        var document = await _htmlParser.ParseDocumentAsync(cachedHtml);
        return GetWord(document, null);
    }

    /// <summary>
    /// Follow the conjugation link, when the entry has one, and record the
    /// verb's forms. Only verbs carry the link, so nothing extra is requested
    /// for other parts of speech.
    /// </summary>
    private async Task AddConjugation(IDocument document, WordParseResult result)
    {
        if (!_options.IncludeConjugations)
        {
            return;
        }

        var conjugationUrl = GetConjugationUrl(document);
        if (conjugationUrl == null)
        {
            return;
        }

        await Task.Delay(_options.RequestDelayMilliseconds);

        try
        {
            var conjugationHtml = await _pageLoader.GetPageAsync(conjugationUrl);
            if (string.IsNullOrEmpty(conjugationHtml))
            {
                return;
            }

            result.ConjugationHtml = conjugationHtml;
            result.ConjugationUrl = conjugationUrl;
            result.Forms = await _conjugationParser.GetFormsAsync(conjugationHtml);

            Console.WriteLine($"Parsed {result.Forms.Count} forms for {result.Word.Headword}");
        }
        catch (Exception ex)
        {
            // A missing conjugation must not lose the entry itself
            Console.WriteLine($"Error parsing conjugation at {conjugationUrl}: {ex.Message}");
        }
    }

    /// <summary>
    /// Find the conjugation page linked from an entry, if the word has one.
    /// Only verbs carry this link, so its absence is how a non-verb is detected.
    /// </summary>
    public static string? GetConjugationUrl(IDocument document)
    {
        var link = document
            .QuerySelectorAll("a.conjugate")
            .FirstOrDefault(a => (a.GetAttribute("href") ?? "").Contains("frverbs.aspx"));

        var href = link?.GetAttribute("href");
        if (string.IsNullOrEmpty(href))
        {
            return null;
        }

        return href.StartsWith("http") ? href : BaseUrl + href;
    }

    public async Task<string?> GetConjugationUrlFromHtml(string html)
    {
        var document = await _htmlParser.ParseDocumentAsync(html);
        return GetConjugationUrl(document);
    }

    private string GetAddress(string searchedWord)
    {
        if (searchedWord.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return searchedWord;
        }

        return DictionaryUrl + Uri.EscapeDataString(searchedWord);
    }

    private Word? GetWord(IDocument document, string? searchedWord)
    {
        var table = GetPrincipalTranslationsTable(document);
        if (table == null)
        {
            return null;
        }

        var entries = ReadEntries(table).Take(_options.MaxTranslations).ToList();
        if (entries.Count == 0)
        {
            return null;
        }

        var headword = entries[0].Headword ?? searchedWord;
        if (string.IsNullOrEmpty(headword))
        {
            return null;
        }

        var senses = entries
            .Select(entry => new Sense
            {
                Definition = entry.BuildDefinition(),
                PartOfSpeech = ParsePartOfSpeech(entry.PartOfSpeech),
                Examples = entry.BuildExamples()
            })
            .Where(sense => !string.IsNullOrWhiteSpace(sense.Definition))
            .ToList();

        if (senses.Count == 0)
        {
            return null;
        }

        return new Word
        {
            Headword = headword,
            Transcription = GetTranscription(document),
            PartOfSpeech = entries[0].PartOfSpeech,
            Language = Language.French,
            Senses = senses,
            Examples = entries.SelectMany(entry => entry.BuildExamples()).ToList()
        };
    }

    /// <summary>
    /// The page holds one table.WRD per section, each opening with its own
    /// header row. td#regular marks the Principal Translations one.
    /// </summary>
    private static IElement? GetPrincipalTranslationsTable(IDocument document)
    {
        var sectionHeader = document.QuerySelector("td#regular");
        var table = sectionHeader?.Closest("table");

        // A word with only one section still renders it as table.WRD
        return table ?? document.QuerySelector("table.WRD");
    }

    /// <summary>
    /// IPA sits above the tables, e.g. &lt;span id='pronWR'&gt;[pʀɑ̃dʀ]&lt;/span&gt;.
    /// </summary>
    private static string? GetTranscription(IDocument document)
    {
        var pronunciation = document.QuerySelector("#pronWR")?.TextContent?.Trim();
        if (string.IsNullOrEmpty(pronunciation))
        {
            return null;
        }

        return pronunciation.Trim('[', ']', ' ');
    }

    private static IEnumerable<WordReferenceEntry> ReadEntries(IElement table)
    {
        WordReferenceEntry? current = null;

        foreach (var row in table.QuerySelectorAll("tr"))
        {
            // Header rows carry the section name and the language captions
            if (row.ClassList.Contains("wrtopsection") || row.ClassList.Contains("langHeader"))
            {
                continue;
            }

            var id = row.GetAttribute("id");
            if (!string.IsNullOrEmpty(id) && id.StartsWith("fren:", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null)
                {
                    yield return current;
                }

                current = ReadEntryRow(row);
                continue;
            }

            // Continuation rows only make sense once an entry has been opened
            if (current != null)
            {
                ReadContinuationRow(row, current);
            }
        }

        if (current != null)
        {
            yield return current;
        }
    }

    private static WordReferenceEntry ReadEntryRow(IElement row)
    {
        var entry = new WordReferenceEntry();

        var frenchCell = row.QuerySelector("td.FrWrd");
        if (frenchCell != null)
        {
            entry.Headword = frenchCell.QuerySelector("strong")?.TextContent?.Trim();
            entry.PartOfSpeech = ReadPartOfSpeech(frenchCell);
        }

        // The sense gloss sits in the middle cell, e.g. "(saisir)"
        entry.Gloss = ReadGloss(row);

        AddTranslation(row, entry);
        return entry;
    }

    private static void ReadContinuationRow(IElement row, WordReferenceEntry entry)
    {
        var frenchCell = row.QuerySelector("td.FrEx");
        if (frenchCell != null)
        {
            entry.FrenchExamples.AddRange(SplitSentences(frenchCell.TextContent));
            return;
        }

        var englishCell = row.QuerySelector("td.ToEx");
        if (englishCell != null)
        {
            entry.EnglishExamples.AddRange(SplitSentences(englishCell.TextContent));
            return;
        }

        AddTranslation(row, entry);
    }

    /// <summary>
    /// A single example cell may hold more than one sentence, and WordReference
    /// is not consistent about how it splits them: one entry puts two French
    /// sentences in one cell and their translations in two rows, the next does
    /// the reverse and separates the pair with "//". Both are split back out so
    /// the two sides can be paired up by position.
    /// </summary>
    private static IEnumerable<string> SplitSentences(string cellText)
    {
        // Non-breaking spaces would otherwise survive into the card text
        var text = WebUtility
            .HtmlDecode(cellText ?? string.Empty)
            .Replace('\u00a0', ' ');

        return Regex
            .Split(text, @"\s*//\s*|\s{2,}")
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0);
    }

    private static void AddTranslation(IElement row, WordReferenceEntry entry)
    {
        var englishCell = row.QuerySelector("td.ToWrd");
        if (englishCell == null)
        {
            return;
        }

        var translation = ReadTranslation(englishCell);
        if (!string.IsNullOrWhiteSpace(translation))
        {
            entry.Translations.Add(translation);
        }
    }

    /// <summary>
    /// A translation cell holds the English word plus a part-of-speech tag and,
    /// for verbs, a conjugation arrow. Only the word itself is wanted.
    /// </summary>
    private static string ReadTranslation(IElement cell)
    {
        var builder = new StringBuilder();

        foreach (var node in cell.ChildNodes)
        {
            if (node is IElement element)
            {
                // POS tags and conjugation links are not part of the translation
                if (element.ClassList.Contains("POS2") || element.ClassList.Contains("conjugate"))
                {
                    continue;
                }

                builder.Append(element.TextContent);
                continue;
            }

            builder.Append(node.TextContent);
        }

        return CollapseWhitespace(builder.ToString());
    }

    private static string? ReadGloss(IElement row)
    {
        // Cells are French | gloss | English, so the gloss is whichever cell is
        // neither of the labelled ones
        var glossCell = row
            .QuerySelectorAll("td")
            .FirstOrDefault(cell =>
                !cell.ClassList.Contains("FrWrd") &&
                !cell.ClassList.Contains("ToWrd") &&
                !cell.ClassList.Contains("FrEx") &&
                !cell.ClassList.Contains("ToEx"));

        var gloss = CollapseWhitespace(glossCell?.TextContent ?? string.Empty);
        return string.IsNullOrWhiteSpace(gloss) ? null : gloss.Trim('(', ')', ' ');
    }

    private static string? ReadPartOfSpeech(IElement cell)
    {
        var abbreviation = cell.QuerySelector("em.POS2")?.GetAttribute("data-abbr");
        if (string.IsNullOrEmpty(abbreviation))
        {
            return cell.QuerySelector("em.POS2")?.TextContent?.Trim();
        }

        // data-abbr is URL encoded and plus-separated, e.g. "vtr+%2B+pr%C3%A9p"
        return CollapseWhitespace(HttpUtility.UrlDecode(abbreviation.Replace('+', ' ')));
    }

    private static PartsOfSpeech ParsePartOfSpeech(string? partOfSpeech)
    {
        if (string.IsNullOrWhiteSpace(partOfSpeech))
        {
            return PartsOfSpeech.Unknown;
        }

        // WordReference abbreviates: nm/nf noun, vtr/vi/v pron verb, adj, adv...
        var value = partOfSpeech.Trim().ToLowerInvariant();

        if (value.StartsWith("adv")) return PartsOfSpeech.Adverb;
        if (value.StartsWith("adj") || value.StartsWith("loc adj")) return PartsOfSpeech.Adjective;
        if (value.StartsWith("nm") || value.StartsWith("nf") || value.StartsWith("n ")) return PartsOfSpeech.Noun;
        if (value.StartsWith("v") || value.StartsWith("loc v")) return PartsOfSpeech.Verb;
        if (value.StartsWith("pron")) return PartsOfSpeech.Pronoun;
        if (value.StartsWith("prép") || value.StartsWith("prep")) return PartsOfSpeech.Preposition;
        if (value.StartsWith("conj")) return PartsOfSpeech.Conjunction;
        if (value.StartsWith("interj")) return PartsOfSpeech.Interjection;
        if (value.StartsWith("art")) return PartsOfSpeech.Article;

        return PartsOfSpeech.Unknown;
    }

    private static string Normalise(string searchedWord)
    {
        return searchedWord.Replace("\r", "").Replace("\n", "").Trim();
    }

    private static string CollapseWhitespace(string value)
    {
        // WordReference pads cells with &nbsp; and newlines
        return string.Join(' ', WebUtility
            .HtmlDecode(value)
            .Replace('\u00a0', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// One entry from the Principal Translations table, before it becomes a Sense.
    /// </summary>
    private class WordReferenceEntry
    {
        public string? Headword { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? Gloss { get; set; }
        public List<string> Translations { get; } = new();
        public List<string> FrenchExamples { get; } = new();
        public List<string> EnglishExamples { get; } = new();

        /// <summary>
        /// Pair each French example with its translation by position, keeping
        /// the "French (English)" convention used elsewhere. A sentence whose
        /// counterpart is missing is kept on its own rather than dropped.
        /// </summary>
        public IList<string> BuildExamples()
        {
            return FrenchExamples
                .Select((french, index) => index < EnglishExamples.Count
                    ? $"{french} ({EnglishExamples[index]})"
                    : french)
                .ToList();
        }

        /// <summary>
        /// "(saisir) take, pick up [sth], grasp" - the French gloss says which
        /// sense is meant, the English translations say what it means.
        /// </summary>
        public string BuildDefinition()
        {
            var translations = string.Join(", ", Translations.Distinct());

            if (string.IsNullOrWhiteSpace(Gloss))
            {
                return translations;
            }

            return string.IsNullOrWhiteSpace(translations)
                ? $"({Gloss})"
                : $"({Gloss}) {translations}";
        }
    }
}
