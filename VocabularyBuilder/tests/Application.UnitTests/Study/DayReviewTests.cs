using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Queries;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class DayReviewTests
{
    [TestCase(0, new int[0])]
    [TestCase(1, new int[0])]
    [TestCase(3, new[] { 3 })]
    [TestCase(6, new[] { 6 })]
    [TestCase(7, new[] { 4, 3 })]   // the one count no split into fours to sixes fits
    [TestCase(8, new[] { 4, 4 })]
    [TestCase(11, new[] { 6, 5 })]
    [TestCase(12, new[] { 6, 6 })]
    [TestCase(13, new[] { 5, 4, 4 })]
    [TestCase(20, new[] { 5, 5, 5, 5 })]
    public void WordsAreSplitIntoTheFewestGroupsOfFourToSix(int words, int[] sizes)
    {
        DayReviewGroups.Sizes(words).Should().Equal(sizes);
    }

    // --- the query ---------------------------------------------------------

    private static readonly DateTimeOffset Now = new(2026, 9, 14, 18, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;

    [SetUp]
    public void SetUp() => _db = new StudyTestContext();

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task Card(string headword, DateTime introduced, CardState state = CardState.Learning, string? sentence = null)
    {
        var word = new Word
        {
            Headword = headword,
            Language = Language.English,
            PartOfSpeech = "adjective",
            Senses = new List<Sense> { new() { Definition = $"the meaning of {headword}", Examples = new List<string>() } }
        };
        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        _db.Context.StudyExamples.Add(new StudyExample
        {
            WordId = word.Id,
            Sentence = sentence ?? $"They saw {headword} yesterday.",
            Form = headword,
            Translation = "Вони бачили."
        });
        _db.Context.ReviewCards.Add(new ReviewCard
        {
            WordId = word.Id,
            State = state,
            CurrentRung = 1,
            IntroducedAtUtc = introduced,
            DueAtUtc = Now.UtcDateTime
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    private Task<DayReviewDto> Review() =>
        new GetDayReviewQueryHandler(_db.Context, new StudyMaterialResolver(), new StudyOptions(), new FakeTimeProvider(Now))
            .Handle(new GetDayReviewQuery(Language.English), CancellationToken.None);

    [Test]
    public async Task TodaysWordsAreGroupedWithTheirSentencesGapped()
    {
        for (var i = 0; i < 7; i++)
        {
            await Card($"word{i}", Now.UtcDateTime.AddHours(-1));
        }

        var review = await Review();

        review.Groups.Select(g => g.Pairs.Count).Should().Equal(4, 3);
        var pair = review.Groups[0].Pairs[0];
        pair.Headword.Should().Be("word0");
        pair.Sentence.Should().Be("They saw _____ yesterday.");
        pair.Translation.Should().Be("Вони бачили.");
    }

    [Test]
    public async Task OnlyWordsMetTodayAreIncluded()
    {
        await Card("today1", Now.UtcDateTime.AddHours(-2));
        await Card("today2", Now.UtcDateTime.AddHours(-1));
        await Card("yesterday", Now.UtcDateTime.AddDays(-1));
        await Card("notmet", Now.UtcDateTime.AddHours(-1), CardState.New);
        await Card("setaside", Now.UtcDateTime.AddHours(-1), CardState.Suspended);

        var review = await Review();

        review.Groups.SelectMany(g => g.Pairs).Select(p => p.Headword).Should().Equal("today1", "today2");
    }

    [Test]
    public async Task AFormTheSentenceUsesIsCarriedForTheMatch()
    {
        await Card("prendre", Now.UtcDateTime.AddHours(-1), sentence: "Elle prend le train.");
        await Card("aller", Now.UtcDateTime.AddHours(-1), sentence: "Nous aller demain.");

        var example = _db.Context.StudyExamples.First(e => e.Sentence == "Elle prend le train.");
        example.Form = "prend";
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var pair = (await Review()).Groups.Single().Pairs.First(p => p.Headword == "prendre");

        pair.Sentence.Should().Be("Elle _____ le train.");
        pair.Form.Should().Be("prend");
    }

    [Test]
    public async Task ASingleWordMakesNoPairsToMatch()
    {
        await Card("lonely", Now.UtcDateTime.AddHours(-1));

        (await Review()).Groups.Should().BeEmpty();
    }
}
