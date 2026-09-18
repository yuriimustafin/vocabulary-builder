using Moq;
using MediatR;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class EnrichWordStudyContentTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;
    private RecordingGptClient _gpt = null!;
    private FakeTimeProvider _clock = null!;
    private StudyOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new StudyTestContext();
        _clock = new FakeTimeProvider(Start);
        _options = new StudyOptions();
        _gpt = new RecordingGptClient(UsableReply);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    /// <summary>A reply whose sentence contains the headword, as a real one must.</summary>
    private static string? UsableReply(string prompt)
    {
        var word = System.Text.RegularExpressions.Regex.Match(prompt, @"word:\s*""([^""]+)""").Groups[1].Value;
        return $$"""{"definition":"a generated definition","sentence":"A line using {{word}} once."}""";
    }

    /// <summary>
    /// The dictionary fill is a separate command and is exercised on its own. Here it reports
    /// that the word already had what it needed, which is what these tests arrange anyway.
    /// </summary>
    private static ISender NoDictionaryFill()
    {
        var sender = new Mock<ISender>();

        sender
            .Setup(s => s.Send(It.IsAny<FillWordFromDictionaryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DictionaryFillOutcome.AlreadyFilled);

        return sender.Object;
    }

    private EnrichWordStudyContentCommandHandler Handler() =>
        new(_db.Context, _gpt, new StudyMaterialResolver(), _options, _clock, NoDictionaryFill());

    private async Task<Word> AddWord(string headword = "ubiquitous", IList<Sense>? senses = null, IList<string>? examples = null)
    {
        var word = new Word
        {
            Headword = headword,
            PartOfSpeech = "adjective",
            Language = Language.English,
            Senses = senses,
            Examples = examples
        };

        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return word;
    }

    private static Sense Sense(string definition, params string[] examples) =>
        new() { Definition = definition, Examples = examples.ToList() };

    private Task<EnrichmentOutcome> Enrich(int wordId) =>
        Handler().Handle(new EnrichWordStudyContentCommand(wordId), CancellationToken.None);

    private Task<WordStudyContent?> Content(int wordId) =>
        _db.Context.WordStudyContents.AsNoTracking().FirstOrDefaultAsync(c => c.WordId == wordId);

    // --- doing nothing where nothing is needed -----------------------------

    [Test]
    public async Task AWordWithDictionaryDataCostsNoCallAtAll()
    {
        var word = await AddWord(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });

        var outcome = await Enrich(word.Id);

        outcome.Should().Be(EnrichmentOutcome.NothingMissing);
        _gpt.CallCount.Should().Be(0);
        (await Content(word.Id)).Should().BeNull("a word missing nothing needs no row");
    }

    [Test]
    public async Task OnlyTheMissingFieldsAreAskedFor()
    {
        // Has a definition, but no example containing the headword.
        var word = await AddWord(senses: new List<Sense> { Sense("found everywhere") });

        await Enrich(word.Id);

        _gpt.LastPrompt.Should().Contain("sentence");
        _gpt.LastPrompt.Should().NotContain("definition");

        var content = await Content(word.Id);
        content!.GeneratedDefinition.Should().BeNull("the word already had one");
        content.GeneratedContextSentence.Should().NotBeNull();
        content.Status.Should().Be(StudyContentStatus.Ready);
    }

    [Test]
    public async Task AWordWithNothingGetsBothFieldsGenerated()
    {
        var word = await AddWord();

        var outcome = await Enrich(word.Id);

        outcome.Should().Be(EnrichmentOutcome.Generated);
        _gpt.LastPrompt.Should().Contain("definition").And.Contain("sentence");

        var content = await Content(word.Id);
        content!.Status.Should().Be(StudyContentStatus.Ready);
        content.GeneratedDefinition.Should().Be("a generated definition");
        content.GeneratedContextSentence.Should().Be("A line using ubiquitous once.");
    }

    // --- idempotency -------------------------------------------------------

    [Test]
    public async Task RunningTwiceGeneratesOnceAndLeavesOneRow()
    {
        var word = await AddWord();

        var first = await Enrich(word.Id);
        var second = await Enrich(word.Id);

        first.Should().Be(EnrichmentOutcome.Generated);
        second.Should().Be(EnrichmentOutcome.AlreadyFilled);
        _gpt.CallCount.Should().Be(1);
        (await _db.Context.WordStudyContents.CountAsync(c => c.WordId == word.Id)).Should().Be(1);
    }

    [Test]
    public async Task ARunInProgressMakesAnotherStandDown()
    {
        var word = await AddWord();

        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Pending,
            ClaimedAtUtc = Start.UtcDateTime
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.ClaimedElsewhere);
        _gpt.CallCount.Should().Be(0);
    }

    [Test]
    public async Task AClaimAbandonedByACrashIsPickedUpAgainOnceItGoesStale()
    {
        var word = await AddWord();

        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Pending,
            ClaimedAtUtc = Start.UtcDateTime
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(_options.EnrichmentStaleClaimMinutes + 1));

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);
        _gpt.CallCount.Should().Be(1);
    }

    [Test]
    public async Task DictionaryDataArrivingLaterClosesTheRowWithoutACall()
    {
        var word = await AddWord();
        await Enrich(word.Id);
        _gpt.Prompts.Clear();

        // The word is filled in from a dictionary after the fact.
        var tracked = await _db.Context.Words.FirstAsync(w => w.Id == word.Id);
        tracked.Senses = new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") };
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.AlreadyFilled);
        _gpt.CallCount.Should().Be(0);
    }

    // --- failure -----------------------------------------------------------

    [Test]
    public async Task AFailedGenerationIsRecordedAndCanBeRetried()
    {
        _gpt = new RecordingGptClient(_ => null);
        var word = await AddWord();

        var outcome = await Enrich(word.Id);

        outcome.Should().Be(EnrichmentOutcome.Failed);

        var content = await Content(word.Id);
        content!.Status.Should().Be(StudyContentStatus.Pending, "it is still worth another attempt");
        content.GenerationAttempts.Should().Be(1);
        content.LastError.Should().NotBeNull();
        content.ClaimedAtUtc.Should().BeNull("a released claim is what lets it be retried");
    }

    [Test]
    public async Task RepeatedFailuresEventuallyStopCostingCalls()
    {
        _gpt = new RecordingGptClient(_ => null);
        var word = await AddWord();

        for (var attempt = 0; attempt < _options.EnrichmentMaxAttempts; attempt++)
        {
            await Enrich(word.Id);
        }

        var callsBeforeGivingUp = _gpt.CallCount;

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.GivenUp);
        _gpt.CallCount.Should().Be(callsBeforeGivingUp, "a word given up on is not asked for again");
        (await Content(word.Id))!.Status.Should().Be(StudyContentStatus.Failed);
    }

    [Test]
    public async Task NonsenseFromTheModelCountsAsAFailure()
    {
        _gpt = new RecordingGptClient(_ => "I'm sorry, I can't help with that.");
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Failed);
    }

    [Test]
    public async Task ASentenceThatDroppedTheWordStillLeavesAStudiableDefinition()
    {
        // Losing the sentence only costs the cloze rung, which the ladder skips anyway,
        // so this is not worth a retry.
        _gpt = new RecordingGptClient(_ =>
            """{"definition":"found everywhere","sentence":"You see them all over the place."}""");
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);

        var content = await Content(word.Id);
        content!.Status.Should().Be(StudyContentStatus.Ready);
        content.GeneratedDefinition.Should().Be("found everywhere");

        new StudyMaterialResolver()
            .Resolve(await _db.Context.Words.Include(w => w.Senses).FirstAsync(w => w.Id == word.Id), content)
            .HasContextSentence.Should().BeFalse();
    }

    [Test]
    public async Task AMissingDefinitionIsAFailureBecauseTheWordCannotBeStudiedAtAll()
    {
        _gpt = new RecordingGptClient(_ => """{"sentence":"A line using ubiquitous once."}""");
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Failed);
    }

    [Test]
    public async Task AModelWrappingItsJsonInProseIsStillUnderstood()
    {
        _gpt = new RecordingGptClient(_ =>
            "Here you go:\n```json\n{\"definition\":\"found everywhere\",\"sentence\":\"Screens are ubiquitous.\"}\n```");
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);
        (await Content(word.Id))!.GeneratedDefinition.Should().Be("found everywhere");
    }

    [Test]
    public async Task AnUnknownWordIsReportedRatherThanThrowing()
    {
        (await Enrich(4242)).Should().Be(EnrichmentOutcome.WordNotFound);
        _gpt.CallCount.Should().Be(0);
    }

    // --- prompt ------------------------------------------------------------

    [Test]
    public void ThePromptCarriesTheMarkerAndTheWordInTheAgreedShape()
    {
        var word = new Word { Headword = "ubiquitous", PartOfSpeech = "adjective", Language = Language.French };

        var prompt = StudyContentPrompt.For(word, StudyMaterialGaps.Meaning);

        prompt.Should().Contain(StudyContentPrompt.Marker);
        prompt.Should().Contain("""word: "ubiquitous" """.TrimEnd());
        prompt.Should().Contain("French");
    }
}
