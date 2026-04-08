using VocabularyBuilder.Application.Lists.Commands;
using VocabularyBuilder.Application.Lists.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

public class Lists : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Map with language parameter in route: /api/{lang}/lists
        var group = app
            .MapGroup("/api/{lang}/lists")
            .WithGroupName("Lists")
            .WithTags("Lists")
            .WithOpenApi();
            
        group.MapGet("/", GetLists);
        group.MapGet("/{id}", GetListDetails)
            .Produces<VocabularyListDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        group.MapPost("/", CreateList);
        group.MapPost("/generate", GenerateListWithAi);
        group.MapPut("/{id}", UpdateList);
        group.MapDelete("/{id}", DeleteList);
        group.MapPost("/{listId}/items", CreateListItem);
        group.MapPut("/{listId}/items/{id}", UpdateListItem);
        group.MapDelete("/{listId}/items/{id}", DeleteListItem);
    }

    public async Task<List<VocabularyListDto>> GetLists(ISender sender, string lang)
    {
        var language = ParseLanguage(lang);
        return await sender.Send(new GetListsQuery(language));
    }

    public async Task<IResult> GetListDetails(ISender sender, string lang, int id)
    {
        var list = await sender.Send(new GetListDetailsQuery(id));
        return list != null ? Results.Ok(list) : Results.NotFound();
    }

    public async Task<int> CreateList(ISender sender, string lang, CreateListCommand command)
    {
        var language = ParseLanguage(lang);
        // Override the language from the route
        var commandWithLanguage = command with { Language = language };
        return await sender.Send(commandWithLanguage);
    }

    public async Task<GeneratedListPreviewDto> GenerateListWithAi(ISender sender, string lang, GenerateListWithAiCommand command)
    {
        var language = ParseLanguage(lang);
        // Override the language from the route
        var commandWithLanguage = command with { Language = language };
        return await sender.Send(commandWithLanguage);
    }

    public async Task<IResult> UpdateList(ISender sender, string lang, int id, UpdateListCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        var success = await sender.Send(command);
        return success ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> DeleteList(ISender sender, string lang, int id)
    {
        var success = await sender.Send(new DeleteListCommand(id));
        return success ? Results.NoContent() : Results.NotFound();
    }

    public async Task<int> CreateListItem(ISender sender, string lang, int listId, CreateListItemCommand command)
    {
        // Ensure the listId from route matches the command
        var commandWithListId = command with { ListId = listId };
        return await sender.Send(commandWithListId);
    }

    public async Task<IResult> UpdateListItem(ISender sender, string lang, int listId, int id, UpdateListItemCommand command)
    {
        if (id != command.Id) return Results.BadRequest();
        var success = await sender.Send(command);
        return success ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> DeleteListItem(ISender sender, string lang, int listId, int id)
    {
        var success = await sender.Send(new DeleteListItemCommand(id));
        return success ? Results.NoContent() : Results.NotFound();
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
