using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp;
using System.Web;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Samples.Entities;
using Microsoft.Identity.Client;

namespace VocabularyBuilder.Infrastructure.Parsers;

// TODO: Create IParser<T> with Parse method returning T collection
// where "Word? GetWord(IDocument document)" -> that Parse method
// TODO: Consider moving that to App layer.
public class OxfordParser : IWordReferenceParser
{
    const string SearchUrl = "https://www.oxfordlearnersdictionaries.com/us/search/english/?q=";
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
            var document = await context.OpenAsync(address);
            var word = GetWord(document);
            if (word != null)
            {
                words.Add(word);
                Console.WriteLine(word.Headword);
            }
        }

        return words;
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
                    .Select(x => x.TextContent);
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
