using VocabularyBuilder.Application.History.Queries;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// The study history read back from the review log - which now outlives the card, so marking
/// a word known or clearing a day leaves the answers readable.
/// </summary>
public class ReviewHistoryTests : BaseTestFixture
{
    private static async Task<ReviewCard> GivenAnsweredWord(string headword, string answer, bool scaffold = false)
    {
        var word = new Word { Headword = headword, Language = Language.French };
        await AddAsync(word);

        var card = new ReviewCard
        {
            WordId = word.Id,
            State = CardState.Review,
            CurrentRung = 2,
            IntervalDays = 3,
            IntroducedAtUtc = DateTime.UtcNow.AddDays(-7),
            DueAtUtc = DateTime.UtcNow
        };
        await AddAsync(card);

        await AddAsync(new ReviewLog
        {
            WordId = word.Id,
            ReviewCardId = card.Id,
            AttemptId = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow,
            ExerciseType = ExerciseType.WordToSpellingCover,
            Grade = ReviewGrade.Hard,
            IsScaffold = scaffold,
            Answer = answer,
            AnswerMatch = "Typo",
            RungBefore = 2,
            RungAfter = 2
        });

        return card;
    }

    [Test]
    public async Task ShouldListAnswersWithTheirWord()
    {
        await GivenAnsweredWord("maison", "maisn");

        var page = await SendAsync(new GetReviewLogQuery { Language = Language.French });

        var entry = page.Items.Single();
        entry.Headword.Should().Be("maison");
        entry.Answer.Should().Be("maisn");
        entry.AnswerMatch.Should().Be("Typo");
        entry.Grade.Should().Be("Hard");
    }

    [Test]
    public async Task ShouldLeaveFollowUpsOutUnlessAskedFor()
    {
        await GivenAnsweredWord("maison", "maison", scaffold: true);

        (await SendAsync(new GetReviewLogQuery { Language = Language.French })).Items.Should().BeEmpty();
        (await SendAsync(new GetReviewLogQuery { Language = Language.French, IncludeFollowUps = true }))
            .Items.Should().ContainSingle();
    }

    [Test]
    public async Task ShouldKeepAWordsAnswersAfterItIsMarkedKnown()
    {
        var card = await GivenAnsweredWord("maison", "maisn");

        await SendAsync(new MarkWordAsKnownCommand(card.Id));

        var history = await SendAsync(new GetWordHistoryQuery(card.WordId));
        history!.Reviews.Should().ContainSingle(r => r.Answer == "maisn" && r.VoidedAtUtc == null);
    }

    [Test]
    public async Task ShouldShowAnswersAClearedDayVoided()
    {
        await GivenAnsweredWord("maison", "maisn");

        await SendAsync(new ClearStudyProgressCommand(Language.French, ClearStudyScope.All));

        var entry = (await SendAsync(new GetReviewLogQuery { Language = Language.French })).Items.Single();
        entry.VoidedAtUtc.Should().NotBeNull();
        entry.VoidReason.Should().Be("ClearedAll");
    }
}
