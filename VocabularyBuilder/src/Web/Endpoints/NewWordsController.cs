using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Exercises.Commands;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

[Route("api/[controller]")]
[ApiController]
// Temporarily this controller will be used for fiddling and debugging the developing system.
public class NewWordsController : ControllerBase
{
    private readonly IWordReferenceParser _wordReferenceParser;
    private readonly IWordsExporter _wordsExporter;
    private readonly ISender _sender;
    
    public NewWordsController(
        IWordReferenceParser wordReferenceParser, 
        IWordsExporter wordsExporter, 
        ISender sender)
    {
        this._wordReferenceParser = wordReferenceParser;
        _wordsExporter = wordsExporter;
        _sender = sender;
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportWordsFromDictionaryResult>> ImportWords([FromQuery] string? listName = null)
    {
        // Read word list from request body
        string unparsedWordList;
        using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            unparsedWordList = await reader.ReadToEndAsync();
        }
        
        var wordsForParsing = unparsedWordList.Split('\n')
            .Select(w => w.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .ToList();

        // Import words using Application layer command
        var result = await _sender.Send(new ImportWordsFromDictionaryCommand
        {
            Words = wordsForParsing,
            ListName = listName,
            SourceType = DictionarySourceType.Oxford
        });

        return Ok(result);
    }

    [HttpGet]
    public async Task<int> SaveWord([FromQuery] CreateWordCommand command)
    {
        return await _sender.Send(command);
    }

    [HttpGet("import-kindle")]
    public async Task<int> ImportKindle([FromQuery] ImportBookWordsCommand command)
    {
        return await _sender.Send(command);
    }


    [HttpPost("audio-text")]
    public async Task<string> GenerateText([FromBody] CreateTextForAudioCommand command)
    {
        return await _sender.Send(command);
    }

    private static string ComputeListHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for readability
    }
}
