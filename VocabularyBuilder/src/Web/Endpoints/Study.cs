using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Application.Study.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

public class Study : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app
            .MapGroup("/api/{lang}/study")
            .WithGroupName("Study")
            .WithTags("Study")
            .WithOpenApi();

        group.MapGet("/queue", GetQueue);
        group.MapGet("/stats", GetStats);
        group.MapPost("/reviews", SubmitReview);
        group.MapPost("/introductions", AcknowledgeIntroduction);
        group.MapPost("/follow-ups", RecordFollowUp);
        group.MapPost("/cards/{id}/suspend", SuspendCard);
        group.MapPost("/cards/{id}/resume", ResumeCard);
        group.MapPost("/cards/{id}/reset", ResetCard);
        group.MapPost("/cards/{id}/known", MarkAsKnown);
    }

    public async Task<StudyQueueDto> GetQueue(ISender sender, string lang, int? limit = null)
    {
        return await sender.Send(new GetStudyQueueQuery(ParseLanguage(lang), limit));
    }

    public async Task<StudyStatsDto> GetStats(ISender sender, string lang)
    {
        return await sender.Send(new GetStudyStatsQuery(ParseLanguage(lang)));
    }

    public async Task<ReviewResultDto> SubmitReview(ISender sender, string lang, SubmitReviewCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IntroductionResultDto> AcknowledgeIntroduction(
        ISender sender, string lang, AcknowledgeIntroductionCommand command)
    {
        return await sender.Send(command);
    }

    public async Task<IResult> RecordFollowUp(ISender sender, string lang, RecordFollowUpCommand command)
    {
        return await sender.Send(command) ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> SuspendCard(ISender sender, string lang, int id)
    {
        return await sender.Send(new SuspendCardCommand(id, true)) ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> ResumeCard(ISender sender, string lang, int id)
    {
        return await sender.Send(new SuspendCardCommand(id, false)) ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> ResetCard(ISender sender, string lang, int id)
    {
        return await sender.Send(new ResetCardCommand(id)) ? Results.NoContent() : Results.NotFound();
    }

    public async Task<IResult> MarkAsKnown(ISender sender, string lang, int id)
    {
        return await sender.Send(new MarkWordAsKnownCommand(id)) ? Results.NoContent() : Results.NotFound();
    }

    private static Language ParseLanguage(string lang) => lang.ToLower() switch
    {
        "en" or "english" => Language.English,
        "fr" or "french" => Language.French,
        _ => Language.English
    };
}
