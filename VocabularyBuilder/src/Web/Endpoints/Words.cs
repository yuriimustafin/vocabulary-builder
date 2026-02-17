using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

public class Words : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .MapGet(GetWords)
            .MapGet(GetWord, "{id}")
            .MapGet(GetWordDetails, "{id}/details")
            .MapPost(CreateWord)
            .MapPut(UpdateWord, "{id}")
            .MapPut(UpdateWordStatus, "{id}/status")
            .MapDelete(DeleteWord, "{id}")
            .MapPost(UpdateWordFrequencies, "update-frequencies")
            .MapGet(GetWordsForExport, "for-export")
            .MapPost(ExportWords, "export");
    }

    public async Task<PaginatedList<WordDto>> GetWords(
        ISender sender, 
        string? sortBy = null,
        int[]? statuses = null,
        int? minEncounterCount = null,
        int? maxEncounterCount = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var statusEnums = statuses?.Select(s => (WordStatus)s).ToList();
        return await sender.Send(new GetWordsQuery(sortBy, statusEnums, minEncounterCount, maxEncounterCount, pageNumber, pageSize));
    }

    public async Task<IResult> GetWord(ISender sender, int id)
    {
        var word = await sender.Send(new GetWordQuery(id));
        return word != null ? Results.Ok(word) : Results.NotFound();
    }

    public async Task<IResult> GetWordDetails(ISender sender, int id)
    {
        var word = await sender.Send(new GetWordDetailsQuery(id));
        return word != null ? Results.Ok(word) : Results.NotFound();
    }

    public async Task<int> CreateWord(ISender sender, CreateWordCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IResult> UpdateWord(ISender sender, int id, UpdateWordCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> UpdateWordStatus(ISender sender, int id, UpdateWordStatusCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    public async Task<IResult> DeleteWord(ISender sender, int id)
    {
        await sender.Send(new DeleteWordCommand(id));
        return Results.NoContent();
    }

    public async Task<UpdateWordFrequenciesResult> UpdateWordFrequencies(ISender sender)
    {
        return await sender.Send(new UpdateWordFrequenciesCommand());
    }

    public async Task<IResult> GetWordsForExport(ISender sender, int[]? statuses = null)
    {
        var statusEnums = statuses?.Select(s => (WordStatus)s).ToList();
        var words = await sender.Send(new GetWordsForExportQuery(statusEnums));
        
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
}
