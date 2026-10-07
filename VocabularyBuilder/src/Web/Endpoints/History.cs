using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.History.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>
/// What happened to the signed-in user's data, and every model and dictionary request made
/// for them. Read-only: the history is written by the actions themselves.
/// </summary>
public class History : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app
            .MapGroup("/api/{lang}/history")
            .WithGroupName("History")
            .WithTags("History")
            .WithOpenApi();

        group.MapGet("/activity", GetActivity);
        group.MapGet("/calls", GetCalls);
        group.MapGet("/calls/{id}", GetCall);
        group.MapGet("/words/{wordId}", GetWordHistory);
    }

    public async Task<PaginatedList<ActivityLogEntryDto>> GetActivity(
        ISender sender,
        string lang,
        string? category = null,
        int? wordId = null,
        int? importId = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        return await sender.Send(new GetActivityLogQuery
        {
            Language = ParseLanguage(lang),
            Category = category,
            WordId = wordId,
            ImportId = importId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    public async Task<PaginatedList<ExternalCallDto>> GetCalls(
        ISender sender,
        string lang,
        ExternalCallProvider? provider = null,
        ExternalCallPurpose? purpose = null,
        bool failedOnly = false,
        int? wordId = null,
        int? importId = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        return await sender.Send(new GetExternalCallsQuery
        {
            Provider = provider,
            Purpose = purpose,
            FailedOnly = failedOnly,
            WordId = wordId,
            ImportId = importId,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    public async Task<IResult> GetCall(ISender sender, string lang, int id)
    {
        var call = await sender.Send(new GetExternalCallQuery(id));
        return call != null ? Results.Ok(call) : Results.NotFound();
    }

    public async Task<IResult> GetWordHistory(ISender sender, string lang, int wordId)
    {
        var history = await sender.Send(new GetWordHistoryQuery(wordId));
        return history != null ? Results.Ok(history) : Results.NotFound();
    }

    private static Language ParseLanguage(string lang)
    {
        return lang.ToLower() switch
        {
            "en" or "english" => Language.English,
            "fr" or "french" => Language.French,
            _ => Language.English
        };
    }
}
