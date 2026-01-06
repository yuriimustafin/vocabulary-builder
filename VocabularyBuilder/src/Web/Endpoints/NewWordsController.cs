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
    
    public NewWordsController(IWordReferenceParser wordReferenceParser, IWordsExporter wordsExporter, ISender sender)
    {
        this._wordReferenceParser = wordReferenceParser;
        _wordsExporter = wordsExporter;
        _sender = sender;
    }

    [HttpPost]
    //[Consumes("text/plain")]
    public async Task<string> LookupWords([FromQuery] string? listName = null)
    {
        // Can be a list of URL to the Oxford dictionary or just words
        string unparsedWordList;
        using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            unparsedWordList = await reader.ReadToEndAsync();
        }
        var wordsForParsing = unparsedWordList.Split('\n');
        var words = await _wordReferenceParser.GetWords(wordsForParsing);

        // Generate source identifier: use listName if provided, otherwise hash of the word list
        var sourceIdentifierBase = !string.IsNullOrWhiteSpace(listName) 
            ? listName 
            : ComputeListHash(unparsedWordList);

        // Save words to the database
        foreach (var word in words)
        {
            await _sender.Send(new UpsertWordCommand
            {
                Headword = word.Headword,
                Transcription = word.Transcription,
                PartOfSpeech = word.PartOfSpeech,
                Frequency = word.Frequency,
                Examples = word.Examples?.ToList(),
                Source = WordEncounterSource.OxfordDictionaryList,
                SourceIdentifier = $"{sourceIdentifierBase}:{word.Headword}",
                Context = !string.IsNullOrWhiteSpace(listName) ? listName : "Oxford Dictionary Import"
            });
        }

        // TODO: FIX THE ISSUE with perpetual response to postman!!!!!
        // it worked with links, and works with 1 word (that is appended to the search URL), but doesn't work with a list of words
        
        var result = _wordsExporter.ExportWords(words);
        Console.WriteLine(result);
        return result;
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
