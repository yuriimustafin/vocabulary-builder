using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Web.Services;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>
/// Test hooks for the study loop. Registered only in the E2ETest environment.
///
/// Spaced repetition plays out over days, and a card's behaviour depends on where it has
/// got to. Driving that entirely through the UI would mean a suite that either waits in
/// real time or only ever tests a word's first minute, so these put a card into a known
/// state directly and let the clock be moved.
/// </summary>
public class StudyTestingEndpoints : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        if (app.Environment.EnvironmentName != "E2ETest")
        {
            return;
        }

        var group = app
            .MapGroup("/api/e2e-testing/study")
            .WithGroupName("E2ETesting")
            .WithTags("E2ETesting")
            .WithOpenApi();

        group.MapPost("/seed-words", SeedWords);
        group.MapPost("/seed-card", SeedCard);
        group.MapPost("/advance-clock", AdvanceClock);
        group.MapPost("/reset-clock", ResetClock);
        group.MapGet("/card/{headword}", GetCard);
    }

    public record SeedWordDto
    {
        public string Headword { get; init; } = string.Empty;
        public string? PartOfSpeech { get; init; } = "adjective";
        public int? Frequency { get; init; }
        public Language Language { get; init; } = Language.English;

        /// <summary>Set to give the word a dictionary sense, so nothing has to be generated.</summary>
        public string? Definition { get; init; }

        /// <summary>Must contain the headword to be usable; the resolver rejects it otherwise.</summary>
        public string? Example { get; init; }

        public int EncounterCount { get; init; }
        public bool IsMarkedForStudy { get; init; }
    }

    public async Task<IResult> SeedWords(
        [FromServices] ApplicationDbContext context, List<SeedWordDto> words)
    {
        var created = new List<object>();

        foreach (var seed in words)
        {
            var word = new Word
            {
                Headword = seed.Headword,
                PartOfSpeech = seed.PartOfSpeech,
                Frequency = seed.Frequency,
                Language = seed.Language,
                IsMarkedForStudy = seed.IsMarkedForStudy
            };

            if (seed.Definition is not null)
            {
                word.Senses = new List<Sense>
                {
                    new()
                    {
                        Definition = seed.Definition,
                        Examples = seed.Example is null ? new List<string>() : new List<string> { seed.Example }
                    }
                };
            }
            else if (seed.Example is not null)
            {
                word.Examples = new List<string> { seed.Example };
            }

            for (var i = 0; i < seed.EncounterCount; i++)
            {
                word.WordEncounters.Add(new WordEncounter
                {
                    Source = WordEncounterSource.Manual,
                    SourceIdentifier = $"seed:{seed.Headword}:{i}"
                });
            }

            context.Words.Add(word);
            await context.SaveChangesAsync();
            created.Add(new { word.Id, word.Headword });
        }

        return Results.Ok(created);
    }

    public record SeedCardDto
    {
        public string Headword { get; init; } = string.Empty;
        public CardState State { get; init; } = CardState.Review;
        public int Rung { get; init; }
        public int IntervalDays { get; init; } = 1;
        public double EaseFactor { get; init; } = 2.5;
        public int Lapses { get; init; }
        public int LapsesSinceRecovery { get; init; }
        public double RecentSuccessRate { get; init; } = 1.0;
        public int ReviewNumber { get; init; }

        /// <summary>Negative puts the card in the past, which is what makes it due.</summary>
        public double DueInDays { get; init; }

        public double? LastReviewedDaysAgo { get; init; }
    }

    public async Task<IResult> SeedCard(
        [FromServices] ApplicationDbContext context,
        [FromServices] TimeProvider timeProvider,
        SeedCardDto seed)
    {
        var word = await context.Words.FirstOrDefaultAsync(w => w.Headword == seed.Headword);

        if (word is null)
        {
            return Results.NotFound(new { error = $"No word '{seed.Headword}'" });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var card = await context.ReviewCards.FirstOrDefaultAsync(c => c.WordId == word.Id)
            ?? new ReviewCard { WordId = word.Id };

        card.State = seed.State;
        card.CurrentRung = seed.Rung;
        card.IntervalDays = seed.IntervalDays;
        card.EaseFactor = seed.EaseFactor;
        card.Lapses = seed.Lapses;
        card.LapsesSinceRecovery = seed.LapsesSinceRecovery;
        card.RecentSuccessRate = seed.RecentSuccessRate;
        card.ReviewNumber = seed.ReviewNumber;
        card.DueAtUtc = now.AddDays(seed.DueInDays);
        card.LastReviewedAtUtc = seed.LastReviewedDaysAgo is { } ago ? now.AddDays(-ago) : null;
        card.IntroducedAtUtc = card.IntroducedAtUtc == default ? now.AddDays(-1) : card.IntroducedAtUtc;

        if (card.Id == 0)
        {
            context.ReviewCards.Add(card);
        }

        await context.SaveChangesAsync();

        return Results.Ok(new { card.Id, card.WordId, card.CurrentRung, card.State });
    }

    public record AdvanceClockDto(double Days = 0, double Minutes = 0);

    public IResult AdvanceClock([FromServices] TimeProvider timeProvider, AdvanceClockDto request)
    {
        if (timeProvider is not TestTimeProvider clock)
        {
            return Results.BadRequest(new { error = "The clock is only movable in the E2ETest environment." });
        }

        clock.Advance(TimeSpan.FromDays(request.Days) + TimeSpan.FromMinutes(request.Minutes));

        return Results.Ok(new { nowUtc = clock.GetUtcNow().UtcDateTime, offset = clock.Offset.ToString() });
    }

    public IResult ResetClock([FromServices] TimeProvider timeProvider)
    {
        if (timeProvider is TestTimeProvider clock)
        {
            clock.Reset();
        }

        return Results.Ok(new { nowUtc = timeProvider.GetUtcNow().UtcDateTime });
    }

    /// <summary>Lets a test assert on where a word has actually got to, not just what it saw.</summary>
    public async Task<IResult> GetCard([FromServices] ApplicationDbContext context, string headword)
    {
        var card = await context.ReviewCards
            .AsNoTracking()
            .Include(c => c.Word)
            .FirstOrDefaultAsync(c => c.Word.Headword == headword);

        if (card is null)
        {
            return Results.NotFound(new { error = $"No card for '{headword}'" });
        }

        var log = await context.ReviewLogs
            .AsNoTracking()
            .Where(l => l.ReviewCardId == card.Id)
            .ToListAsync();

        return Results.Ok(new
        {
            card.Id,
            card.WordId,
            headword,
            state = card.State,
            rung = card.CurrentRung,
            card.IntervalDays,
            card.EaseFactor,
            card.Lapses,
            card.LapsesSinceRecovery,
            card.RecentSuccessRate,
            card.ReviewNumber,
            card.LearningStepIndex,
            card.DueAtUtc,
            card.LastReviewedAtUtc,
            gradedReviews = log.Count(l => !l.IsScaffold),
            followUps = log.Count(l => l.IsScaffold)
        });
    }
}
