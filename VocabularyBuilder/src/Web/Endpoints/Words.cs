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
            .MapPost(UpdateWordFrequencies, "update-frequencies");
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
}
