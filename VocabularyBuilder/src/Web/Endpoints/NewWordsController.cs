using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;

namespace VocabularyBuilder.Web.Endpoints;

[Route("api/[controller]")]
[ApiController]
public class NewWordsController : ControllerBase
{
    private readonly IWordReferenceParser _wordReferenceParser;
    private readonly IWordsExporter _wordsExporter;
    public NewWordsController(IWordReferenceParser wordReferenceParser, IWordsExporter wordsExporter)
    {
        this._wordReferenceParser = wordReferenceParser;
        _wordsExporter = wordsExporter;
    }

    [HttpPost]
    //[Consumes("text/plain")]
    public async Task<string> LookupWords()
    {
        string wordList;
        using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            wordList = await reader.ReadToEndAsync();
        }
        var wordsForParsing = wordList.Split('\n');
        var words = await _wordReferenceParser.GetWords(wordsForParsing);
        return _wordsExporter.ExportWords(words);
    }
}
