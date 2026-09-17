using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>
/// Throwing away study progress so a session can be tried again.
///
/// Registered only outside Production. These delete real rows, and nothing about a study
/// session is worth that risk against a live collection - the guard is here rather than in
/// the handler so the route simply does not exist where it should not.
/// </summary>
public class StudyDevEndpoints : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            return;
        }

        var group = app
            .MapGroup("/api/{lang}/study/dev")
            .WithGroupName("Study")
            .WithTags("StudyDev")
            .WithOpenApi();

        group.MapPost("/clear-today", ClearToday);
        group.MapPost("/clear-all", ClearAll);
    }

    public async Task<ClearStudyProgressResultDto> ClearToday(ISender sender, string lang)
    {
        return await sender.Send(new ClearStudyProgressCommand(ParseLanguage(lang), ClearStudyScope.Today));
    }

    public async Task<ClearStudyProgressResultDto> ClearAll(ISender sender, string lang)
    {
        return await sender.Send(new ClearStudyProgressCommand(ParseLanguage(lang), ClearStudyScope.All));
    }

    private static Language ParseLanguage(string lang) => lang.ToLower() switch
    {
        "en" or "english" => Language.English,
        "fr" or "french" => Language.French,
        _ => Language.English
    };
}
