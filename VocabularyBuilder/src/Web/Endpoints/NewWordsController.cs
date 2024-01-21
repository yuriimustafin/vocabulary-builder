using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace VocabularyBuilder.Web.Endpoints;

[Route("api/[controller]")]
[ApiController]
// Temporarily this controller will be used for fiddling and debugging the developing system.
public class NewWordsController : ControllerBase
{
    private readonly IWordReferenceParser _wordReferenceParser;
    private readonly IWordsExporter _wordsExporter;
    private readonly ISender _sender;
    public NewWordsController(IWordReferenceParser wordReferenceParser, IWordsExporter wordsExporter, ISender sender)
    {
        this._wordReferenceParser = wordReferenceParser;
        _wordsExporter = wordsExporter;
        _sender = sender;
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

    [HttpGet]
    public async Task<string> SaveWord([FromQuery] CreateWordCommand command)
    {
        return await _sender.Send(command);
    }

    [HttpGet("import-kindle")]
    public async Task<int> ImportKindle([FromQuery] ImportBookWordsCommand command)
    {
        return await _sender.Send(command);
    }
}
