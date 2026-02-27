using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

public class Words : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Map with language parameter in route: /api/{lang}/words
        var group = app
            .MapGroup("/api/{lang}/words")
            .WithGroupName("Words")
            .WithTags("Words")
            .WithOpenApi();
            
        group.MapGet("/", GetWords);
        group.MapGet("/{id}", GetWord);
        group.MapGet("/{id}/details", GetWordDetails);
        group.MapPost("/", CreateWord);
        group.MapPut("/{id}", UpdateWord);
        group.MapPut("/{id}/status", UpdateWordStatus);
        group.MapDelete("/{id}", DeleteWord);
        group.MapPost("/update-frequencies", UpdateWordFrequencies);
        group.MapGet("/for-export", GetWordsForExport);
        group.MapPost("/export", ExportWords);
    }

    public async Task<PaginatedList<WordDto>> GetWords(
        ISender sender,
        string lang,
        string? sortBy = null,
        int[]? statuses = null,
        int? minEncounterCount = null,
        int? maxEncounterCount = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var language = ParseLanguage(lang);
        var statusEnums = statuses?.Select(s => (WordStatus)s).ToList();
        return await sender.Send(new GetWordsQuery(language, sortBy, statusEnums, minEncounterCount, maxEncounterCount, pageNumber, pageSize));
    }

    public async Task<IResult> GetWord(ISender sender, string lang, int id)
    {
        var word = await sender.Send(new GetWordQuery(id));
        return word != null ? Results.Ok(word) : Results.NotFound();
    }

    public async Task<IResult> GetWordDetails(ISender sender, string lang, int id)
    {
        var word = await sender.Send(new GetWordDetailsQuery(id));
        return word != null ? Results.Ok(word) : Results.NotFound();
    }

    public async Task<int> CreateWord(ISender sender, string lang, CreateWordCommand command)
    {
        var language = ParseLanguage(lang);
        // Override the language from the route
        var commandWithLanguage = command with { Language = language };
        return await sender.Send(commandWithLanguage);
    }

    public async Task<IResult> UpdateWord(ISender sender, string lang, int id, UpdateWordCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> UpdateWordStatus(ISender sender, string lang, int id, UpdateWordStatusCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteWord(ISender sender, string lang, int id)
    {
        await sender.Send(new DeleteWordCommand(id));
        return Results.NoContent();
    }

    public async Task<UpdateWordFrequenciesResult> UpdateWordFrequencies(ISender sender, string lang)
    {
        return await sender.Send(new UpdateWordFrequenciesCommand());
    }

    public async Task<IResult> GetWordsForExport(ISender sender, string lang, int[]? statuses = null)
    {
        var language = ParseLanguage(lang);
        var statusEnums = statuses?.Select(s => (WordStatus)s).ToList();
        var words = await sender.Send(new GetWordsForExportQuery(language, statusEnums));
        
        // Return simplified word info for preview
        var wordsPreview = words.Select(w => new
        {
            w.Id,
            w.Headword,
            w.PartOfSpeech,
            w.Status,
            SenseCount = w.Senses?.Count() ?? 0
        }).ToList();
        
        return Results.Ok(wordsPreview);
    }

    public async Task<IResult> ExportWords(ISender sender, ExportWordsCommand command)
    {
        var result = await sender.Send(command);
        
        if (result.ExportedCount == 0)
        {
            return Results.BadRequest("No words to export");
        }

        // Return CSV content with appropriate headers for download
        var csvBytes = System.Text.Encoding.UTF8.GetBytes(result.CsvContent);
        var fileName = $"anki-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        
        return Results.File(csvBytes, "text/csv", fileName);
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
