using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp;
using AngleSharp.Html.Parser;
using System.Web;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using Microsoft.Identity.Client;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities.History;

namespace VocabularyBuilder.Infrastructure.Parsers;

// TODO: Create IParser<T> with Parse method returning T collection
// where "Word? GetWord(IDocument document)" -> that Parse method
// TODO: Consider moving that to App layer.
public class OxfordParser : IWordReferenceParser
{
    const string SearchUrl = "https://www.oxfordlearnersdictionaries.com/us/search/english/?q=";

    private readonly IExternalCallRecorder? _recorder;

    /// <param name="recorder">Where each page fetch is logged. Left out by the mock, which fetches nothing.</param>
    public OxfordParser(IExternalCallRecorder? recorder = null)
    {
        _recorder = recorder;
    }

    public DictionarySourceType SourceType => DictionarySourceType.Oxford;
    
    public async Task<IEnumerable<Word>> GetWords(IEnumerable<string> searchedWords)
    {
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var words = new List<Word>();

        foreach (var searchedWord in searchedWords)
        {
            //https://www.oxfordlearnersdictionaries.com/us/search/english/?q=grate
            //var address = "https://www.oxfordlearnersdictionaries.com/us/definition/english/grate_1?q=grate";

            //https://www.oxfordlearnersdictionaries.com/us/definition/english/grade_1?q=grade - FIX: idioms included as senses
            // for now searchedWord is a URL
            var address = HttpUtility.UrlDecode(GetAddress(searchedWord));
            var document = await OpenAsync(context, address, searchedWord);
            var word = GetWord(document);
            if (word != null)
            {
                words.Add(word);
                Console.WriteLine(word.Headword);
            }
        }

        return words;
    }

    /// <summary>
    /// Opens the page and logs the fetch - its URL, status and size, not the page, which is
    /// cached against the word when it parses.
    /// </summary>
    private async Task<IDocument> OpenAsync(IBrowsingContext context, string address, string searchedWord)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        IDocument document;

        try
        {
            document = await context.OpenAsync(address);
        }
        catch (Exception ex)
        {
            await Record(address, searchedWord, startedAt, stopwatch, null, null, ex.Message);
            throw;
        }

        var statusCode = (int)document.StatusCode;
        var length = document.DocumentElement?.OuterHtml.Length;
        var succeeded = statusCode is >= 200 and < 300;

        await Record(address, searchedWord, startedAt, stopwatch, statusCode, length,
            succeeded ? null : $"Oxford returned {statusCode}");

        return document;
    }

    private Task Record(
        string address, string searchedWord, DateTime startedAt, System.Diagnostics.Stopwatch stopwatch,
        int? statusCode, int? length, string? error)
    {
        if (_recorder is null)
        {
            return Task.CompletedTask;
        }

        return _recorder.RecordAsync(new ExternalCallLog
        {
            StartedAtUtc = startedAt,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Provider = ExternalCallProvider.Oxford,
            Purpose = ExternalCallPurpose.DictionaryEntry,
            Target = searchedWord,
            Url = address,
            StatusCode = statusCode,
            ResponseLength = length,
            Succeeded = error is null,
            Error = error
        });
    }

    /// <summary>
    /// Get words with their source HTML and URL for caching
    /// </summary>
    public async Task<IEnumerable<WordParseResult>> GetWordsWithSource(IEnumerable<string> searchedWords)
    {
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var results = new List<WordParseResult>();

        foreach (var searchedWord in searchedWords)
        {
            var address = HttpUtility.UrlDecode(GetAddress(searchedWord));
            var document = await OpenAsync(context, address, searchedWord);
            var word = GetWord(document);
            if (word != null)
            {
                results.Add(new WordParseResult
                {
                    Word = word,
                    SearchedTerm = searchedWord,
                    SourceHtml = document.DocumentElement.OuterHtml,
                    SourceUrl = address
                });
                Console.WriteLine(word.Headword);
            }
        }

        return results;
    }

    /// <summary>
    /// Parse word from cached HTML content instead of fetching from Oxford
    /// </summary>
    public async Task<Word?> GetWordFromCachedHtml(string cachedHtml)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(cachedHtml);
        return GetWord(document);
    }

    private string GetAddress(string searchedWord)
    {
        searchedWord = searchedWord.Trim().Replace("\r", "").Replace("\n", "").Trim();
        if (searchedWord.Contains("https://"))
        {
            return searchedWord;
        }
        return SearchUrl + searchedWord;
    }

    // Consider using these code outside of the class and receive String as an input:
    /*
        string url = "http://example.com"; // Replace with your URL
        HttpClient client = new HttpClient();

        try
        {
            string htmlContent = await client.GetStringAsync(url);
            await ParseHtmlContent(htmlContent);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error fetching the HTML content: {e.Message}");
        }
     -------
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);
     */

    private Word? GetWord(IDocument document)
    {
        try
        {
            var headword = document.QuerySelector("h1")?.TextContent;
            if (headword == null)
            {
                return null;
            }

            var transcription = document.QuerySelector(".phons_n_am .phon")?.TextContent;
            var partOfSpeech = document.QuerySelector(".webtop .pos")?.TextContent;

            var senses = new List<Sense>();
            var senseElements = document.QuerySelectorAll("li.sense");
            foreach (var senseElement in senseElements)
            {
                var def = senseElement.QuerySelector(".def")?.TextContent;
                var examples = senseElement
                    .QuerySelectorAll("ul.examples li")
                    .Select(x => x.TextContent)
                    .ToList();
                if (def is not null)
                {
                    senses.Add(
                        new Sense()
                        {
                            Definition = def,
                            Examples = examples
                        });
                }
            }
            return new Word()
            {
                Headword = headword,
                Transcription = transcription,
                PartOfSpeech = partOfSpeech,
                Senses = senses
            };
        }
        catch 
        {
            Console.WriteLine(document.Url);
            return null;
        }
    }

}
