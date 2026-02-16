using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;

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
            .MapDelete(DeleteWord, "{id}")
            .MapPost(UpdateWordFrequencies, "update-frequencies");
    }

    public async Task<List<WordDto>> GetWords(ISender sender)
    {
        return await sender.Send(new GetWordsQuery());
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
