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
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Web.Endpoints;

[Route("api/[controller]")]
[ApiController]
// Temporarily this controller will be used for fiddling and debugging the developing system.
public class NewWordsController : ControllerBase
{
    private readonly IWordsExporter _wordsExporter;
    private readonly ISender _sender;
    
    public NewWordsController(
        IWordsExporter wordsExporter, 
        ISender sender)
    {
        _wordsExporter = wordsExporter;
        _sender = sender;
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportWordsFromDictionaryResult>> ImportWords(
        [FromQuery] string? listName = null,
        [FromQuery] string lang = "en",
        [FromQuery] DictionarySourceType? sourceType = null,
        [FromQuery] bool? parseImmediately = null)
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

        // Parse language
        var language = ParseLanguage(lang);
        
        // Detect if these are URLs - if any word starts with https://, treat as URLs
        bool areUrls = wordsForParsing.Any(w => w.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        // Determine source type: use explicit if provided, otherwise infer from language
        var dictSourceType = sourceType ?? language.GetDefaultSourceType();

        // Determine parseImmediately: use explicit if provided, otherwise auto-detect from URLs
        bool shouldParseImmediately = parseImmediately ?? areUrls;

        // Import words using Application layer command
        var result = await _sender.Send(new ImportWordsFromDictionaryCommand
        {
            Words = wordsForParsing,
            ListName = listName,
            Language = language,
            SourceType = dictSourceType,
            ParseImmediately = shouldParseImmediately
        });

        return Ok(result);
    }

    [HttpGet]
    public async Task<int> SaveWord([FromQuery] CreateWordCommand command)
    {
        return await _sender.Send(command);
    }

    [HttpPost("import-kindle")]
    public async Task<ActionResult<int>> ImportKindle([FromForm] IFormFile file, [FromQuery] string lang = "en")
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        string fileContent;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            fileContent = await reader.ReadToEndAsync();
        }

        var language = ParseLanguage(lang);
        var result = await _sender.Send(new ImportBookWordsCommand(fileContent, language));
        return Ok(result);
    }

    [HttpPost("import-lingq")]
    public async Task<ActionResult<VocabularyImportResult>> ImportLingQ(
        [FromForm] IFormFile file,
        [FromQuery] string lang = "fr",
        [FromQuery] string? listName = null,
        [FromQuery] string? tag = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        string fileContent;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            fileContent = await reader.ReadToEndAsync();
        }

        var result = await _sender.Send(new ImportLingQWordsCommand
        {
            FileContent = fileContent,
            Language = ParseLanguage(lang),
            ListName = listName,
            Tag = tag
        });

        return Ok(result);
    }

    [HttpPost("import-notes")]
    public async Task<ActionResult<VocabularyImportResult>> ImportNotes(
        [FromQuery] string lang = "fr",
        [FromQuery] string? listName = null,
        [FromQuery] string? tag = null)
    {
        string notes;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            notes = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            return BadRequest("No notes provided");
        }

        var result = await _sender.Send(new ImportLessonNotesCommand
        {
            Notes = notes,
            Language = ParseLanguage(lang),
            ListName = listName,
            Tag = tag
        });

        return Ok(result);
    }


    [HttpPost("import-frequency")]
    public async Task<ActionResult<int>> ImportFrequency([FromQuery] string filePath, [FromQuery] string lang = "en")
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest("File path is required");
        }

        if (!System.IO.File.Exists(filePath))
        {
            return BadRequest($"File not found: {filePath}");
        }

        var language = ParseLanguage(lang);
        var result = await _sender.Send(new ImportFrequencyWordsCommand(filePath, language));
        return Ok(result);
    }


    [HttpPost("audio-text")]
    public async Task<string> GenerateText([FromBody] CreateTextForAudioCommand command, [FromQuery] string lang = "en")
    {
        // Override the language from the query string, as the other endpoints do
        var commandWithLanguage = command with { Language = ParseLanguage(lang) };
        return await _sender.Send(commandWithLanguage);
    }

    private static string ComputeListHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for readability
    }
    
    private static Language ParseLanguage(string lang)
    {
        return lang.ToLower() switch
        {
            "en" or "english" => Language.English,
            "fr" or "french" => Language.French,
            _ => Language.English // Default to English
        };
    }
}
