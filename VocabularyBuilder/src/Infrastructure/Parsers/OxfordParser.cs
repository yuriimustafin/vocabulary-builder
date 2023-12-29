using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp;
using System.Web;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;
public class OxfordParser : IWordReferenceParser
{
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
            var address = HttpUtility.UrlDecode(searchedWord);
            var document = await context.OpenAsync(address);
            var title = document.Title;
            var word = GetWord(document);
            if (word != null)
            {
                words.Add(word);
            }
        }

        return words;
    }

    private Word? GetWord(IDocument document)
    {
        var headword = document.QuerySelector("h1")?.TextContent;
        if (headword == null)
        {
            return null;
        }

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
            Senses = senses
        };
    }

}
