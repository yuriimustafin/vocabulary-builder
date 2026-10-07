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

    /// <summary>A full reply whose examples contain the headword, as a real one's must.</summary>
    private static string? UsableReply(string prompt)
    {
        var word = System.Text.RegularExpressions.Regex.Match(prompt, @"word:\s*""([^""]+)""").Groups[1].Value;

        return $$"""
            {
              "definition": "a generated definition",
              "usage": "said of things found everywhere",
              "examples": [
                {"sentence": "A line using {{word}} once.", "translation": "Рядок.", "form": "{{word}}", "collocation": "using {{word}}",
                 "glosses": [{"word": "A", "translation": "один"}, {"word": "line", "translation": "рядок"}, {"word": "  "}]},
                {"sentence": "Coffee shops are {{word}} here.", "translation": "Кав'ярні.", "form": "{{word}}", "collocation": "{{word}} here"},
                {"sentence": "The {{word}} smartphone changed us.", "translation": "Смартфон.", "form": "{{word}}", "collocation": "the {{word}} smartphone"}
              ],
              "etymology": "From Latin ubique, everywhere.",
              "cognates": null,
              "mnemonic": "Sounds like 'you bake it us': bread baked everywhere."
            }
            """;
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

    private Task<List<StudyExample>> Examples(int wordId) =>
        _db.Context.StudyExamples.AsNoTracking().Where(e => e.WordId == wordId).OrderBy(e => e.Id).ToListAsync();

    /// <summary>Content already generated from the current prompt, with its three examples.</summary>
    private async Task FillCurrent(int wordId)
    {
        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = wordId,
            Status = StudyContentStatus.Ready,
            PromptVersion = StudyContentPrompt.Version
        });

        foreach (var sentence in new[] { "Screens are ubiquitous.", "Ubiquitous ads.", "It is ubiquitous here." })
        {
            _db.Context.StudyExamples.Add(new StudyExample { WordId = wordId, Sentence = sentence, Form = "ubiquitous" });
        }

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    // --- doing nothing where nothing is needed -----------------------------

    [Test]
    public async Task AWordFilledInForTheCurrentPromptCostsNoCallAtAll()
    {
        var word = await AddWord(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });
        await FillCurrent(word.Id);

        var outcome = await Enrich(word.Id);

        outcome.Should().Be(EnrichmentOutcome.AlreadyFilled);
        _gpt.CallCount.Should().Be(0);
    }

    [Test]
    public async Task AWordWithDictionaryDataIsAskedForExamplesAndConnectionsButNotADefinition()
    {
        var word = await AddWord(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);

        _gpt.LastPrompt.Should().Contain("\"examples\"").And.Contain("\"etymology\"").And.Contain("\"mnemonic\"")
            .And.Contain("\"glosses\"").And.Contain("2 to 3");
        _gpt.LastPrompt.Should().NotContain("\"definition\"", "the word already has one");
        _gpt.LastPrompt.Should().Contain("meaning: found everywhere", "the examples should be for the sense being studied");

        var content = await Content(word.Id);
        content!.GeneratedDefinition.Should().BeNull();
        content.Status.Should().Be(StudyContentStatus.Ready);
        content.PromptVersion.Should().Be(StudyContentPrompt.Version);
    }

    [Test]
    public async Task AGenerationWithoutTheDefinitionItNeededIsNotStampedWithThePromptVersion()
    {
        // Everything but the one thing the word could not be studied without
        _gpt = new RecordingGptClient(prompt => UsableReply(prompt)!.Replace("\"a generated definition\"", "null"));
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Failed);

        var content = await Content(word.Id);
        content!.Status.Should().Be(StudyContentStatus.Pending);
        content.PromptVersion.Should().NotBe(StudyContentPrompt.Version,
            "the version is written only when a generation succeeds");

        // So the retry is asked for everything again, the connections included
        _gpt = new RecordingGptClient(UsableReply);
        _clock.Advance(TimeSpan.FromMinutes(_options.EnrichmentStaleClaimMinutes + 1));

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);
        _gpt.LastPrompt.Should().Contain("\"definition\"").And.Contain("\"etymology\"");
        (await Content(word.Id))!.PromptVersion.Should().Be(StudyContentPrompt.Version);
    }

    [Test]
    public async Task AWordWithNothingGetsEverythingGenerated()
    {
        var word = await AddWord();

        var outcome = await Enrich(word.Id);

        outcome.Should().Be(EnrichmentOutcome.Generated);
        _gpt.LastPrompt.Should().Contain("\"definition\"").And.Contain("\"examples\"").And.Contain("\"usage\"");

        var content = await Content(word.Id);
        content!.Status.Should().Be(StudyContentStatus.Ready);
        content.GeneratedDefinition.Should().Be("a generated definition");
        content.Usage.Should().Be("said of things found everywhere");
        content.Etymology.Should().Be("From Latin ubique, everywhere.");
        content.Cognates.Should().BeNull("the model had none, and said so");
        content.Mnemonic.Should().StartWith("Sounds like");

        // Each example keeps what its words mean, for the hints on a sentence's pieces; a
        // blank word is dropped with its translation, so the two stay paired
        var first = (await Examples(word.Id))[0];
        first.GlossWords.Should().Equal("A", "line");
        first.GlossTranslations.Should().Equal("один", "рядок");
    }

    [Test]
    public async Task ExamplesAreStoredWithTheirFormCollocationAndTranslation()
    {
        var word = await AddWord();

        await Enrich(word.Id);

        var examples = await Examples(word.Id);
        examples.Should().HaveCount(3);
        examples[1].Sentence.Should().Be("Coffee shops are ubiquitous here.");
        examples[1].Form.Should().Be("ubiquitous");
        examples[1].Collocation.Should().Be("ubiquitous here");
        examples[1].Translation.Should().Be("Кав'ярні.");
        examples.Should().OnlyContain(e => e.Successes == 0);
    }

    [Test]
    public async Task AnExampleThatDoesNotContainItsFormIsDropped()
    {
        _gpt = new RecordingGptClient(_ => """
            {"definition": "found everywhere", "examples": [
              {"sentence": "You see them all over.", "form": "ubiquitous"},
              {"sentence": "Screens are ubiquitous.", "form": "ubiquitous"},
              {"sentence": "Screens are ubiquitous.", "form": "ubiquitous"}
            ]}
            """);
        var word = await AddWord();

        await Enrich(word.Id);

        (await Examples(word.Id)).Select(e => e.Sentence).Should().Equal("Screens are ubiquitous.");
    }

    [Test]
    public async Task AFormTheWordWasMetInIsAskedForAndItsExampleKept()
    {
        _gpt = new RecordingGptClient(_ => """
            {"examples": [{"sentence": "Elle prend le train.", "translation": "She takes the train.", "form": "prend", "collocation": "prendre le train"}]}
            """);
        var word = await AddWord("prendre", senses: new List<Sense> { Sense("to take", "Je vais prendre le bus.") });
        await FillCurrent(word.Id);

        _db.Context.WordEncounters.Add(new WordEncounter { WordId = word.Id, Source = WordEncounterSource.LingQ, Form = "prend" });
        var content = await _db.Context.WordStudyContents.FirstAsync(c => c.WordId == word.Id);
        content.Status = StudyContentStatus.Pending;
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);

        _gpt.LastPrompt.Should().Contain("\"prend\"");
        _gpt.LastPrompt.Should().NotContain("\"etymology\"", "the connections were already generated");
        (await Examples(word.Id)).Should().Contain(e => e.Form == "prend" && e.Sentence == "Elle prend le train.");
    }

    [Test]
    public async Task ContentFromAnOlderPromptIsAskedOnceForWhatTheNewOneAdds()
    {
        var word = await AddWord();
        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = "found everywhere",
            PromptVersion = "v2"
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);
        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.AlreadyFilled);

        _gpt.CallCount.Should().Be(1);
        _gpt.LastPrompt.Should().NotContain("\"definition\"");
        (await Content(word.Id))!.Etymology.Should().NotBeNull();
    }

    [Test]
    public async Task AFailedFirstAttemptIsStillAskedForEverythingNextTime()
    {
        var replies = new Queue<string?>(new[] { null, UsableReply("word: \"ubiquitous\"") });
        _gpt = new RecordingGptClient(_ => replies.Dequeue());
        var word = await AddWord();

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Failed);
        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);

        _gpt.LastPrompt.Should().Contain("\"etymology\"");
        (await Content(word.Id))!.PromptVersion.Should().Be(StudyContentPrompt.Version);
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
        // Losing the sentence only costs the cloze exercises, which the ladder skips anyway,
        // so this is not worth a retry.
        _gpt = new RecordingGptClient(_ =>
            """{"definition":"found everywhere","examples":[{"sentence":"You see them all over the place.","form":"ubiquitous"}]}""");
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
    public async Task StoredSentencesWithoutGlossesAreGlossedOnTheNextGeneration()
    {
        // Content from before glosses were asked for: two sentences stored, neither glossed
        var word = await AddWord(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });
        _db.Context.WordStudyContents.Add(new WordStudyContent { WordId = word.Id, Status = StudyContentStatus.Ready, PromptVersion = "v5" });
        _db.Context.StudyExamples.AddRange(
            new StudyExample { WordId = word.Id, Sentence = "Screens are ubiquitous.", Translation = "Екрани всюди.", Form = "ubiquitous" },
            new StudyExample { WordId = word.Id, Sentence = "Ads are ubiquitous now.", Translation = "Реклама всюди.", Form = "ubiquitous" });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        _gpt = new RecordingGptClient(_ => """
            {
              "usage": "said of things found everywhere",
              "glosses": [
                [{"word": "Screens", "translation": "екрани"}, {"word": "are", "translation": "є"}, {"word": "ubiquitous.", "translation": "всюдисущі"}],
                [{"word": "Ads", "translation": "реклама"}]
              ]
            }
            """);

        (await Enrich(word.Id)).Should().Be(EnrichmentOutcome.Generated);

        _gpt.LastPrompt.Should().Contain("\"glosses\": an array with one entry for each of these sentences, in this order")
            .And.Contain("1. \"Screens are ubiquitous.\"")
            .And.Contain("2. \"Ads are ubiquitous now.\"");

        var examples = await Examples(word.Id);
        examples[0].GlossWords.Should().Equal("Screens", "are", "ubiquitous.");
        examples[0].GlossTranslations.Should().Equal("екрани", "є", "всюдисущі");
        examples[1].GlossWords.Should().Equal("Ads");
    }

    [Test]
    public void ThePromptAsksForEveryExampleWordByWord()
    {
        var word = new Word { Headword = "lumineux", PartOfSpeech = "adjective", Language = Language.French };

        var prompt = StudyContentPrompt.For(word, StudyMaterialGaps.Examples);

        prompt.Should().Contain("\"sentence\", \"translation\", \"form\", \"collocation\", \"glosses\"");
        prompt.Should().Contain("\"glosses\" is every word of the sentence in order");
        prompt.Should().Contain("what it means there in English");
        prompt.Should().NotContain("\"collocates\"", "what it goes with is no longer asked");
    }

    /// <summary>
    /// What a real model got wrong: it copied the prompt's own examples for a word the prompt
    /// used as one, left articles out, wrote a collocation in English, and gave no cognates
    /// for a word whose etymology named some.
    /// </summary>
    [Test]
    public void ThePromptGuardsAgainstWhatARealModelGotWrong()
    {
        var word = new Word { Headword = "prendre", PartOfSpeech = "verb", Language = Language.French };

        var prompt = StudyContentPrompt.For(word, StudyMaterialGaps.Connections | StudyMaterialGaps.Examples);

        prompt.Should().NotContain("\"le bus\"", "a word the prompt uses as its example gets that example back");
        prompt.Should().Contain("Never copy them into yours - answer for \"prendre\" itself");
        prompt.Should().Contain("\"collocation\" is the French phrase");
        prompt.Should().Contain("grammatically complete French - articles included");
        prompt.Should().Contain("including any the etymology names");
    }

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
