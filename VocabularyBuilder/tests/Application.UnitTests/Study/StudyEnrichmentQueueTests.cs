using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.HttpClients;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class StudyEnrichmentQueueTests
{
    [Test]
    public async Task WordsComeBackOutInTheOrderTheyWentIn()
    {
        var queue = new StudyEnrichmentQueue();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        using var cancellation = new CancellationTokenSource();
        var read = new List<int>();

        await foreach (var wordId in queue.ReadAllAsync(cancellation.Token))
        {
            read.Add(wordId);
            if (read.Count == 3)
            {
                cancellation.Cancel();
                break;
            }
        }

        read.Should().Equal(1, 2, 3);
    }

    [Test]
    public void TheWaitingCountTracksWhatHasBeenAskedFor()
    {
        var queue = new StudyEnrichmentQueue();

        queue.PendingCount.Should().Be(0);

        queue.Enqueue(1);
        queue.Enqueue(2);

        queue.PendingCount.Should().Be(2);
    }

    [Test]
    public async Task ReadingDrainsTheWaitingCount()
    {
        var queue = new StudyEnrichmentQueue();
        queue.Enqueue(1);

        using var cancellation = new CancellationTokenSource();

        await foreach (var _ in queue.ReadAllAsync(cancellation.Token))
        {
            cancellation.Cancel();
            break;
        }

        queue.PendingCount.Should().Be(0);
    }

    [Test]
    public void AWordAlreadyWaitingIsNotQueuedAgain()
    {
        // Every fetch of a session asks again for every word on it not yet filled in.
        var queue = new StudyEnrichmentQueue();

        queue.Enqueue(7);
        queue.Enqueue(7);
        queue.Enqueue(8);

        queue.PendingCount.Should().Be(2);
    }

    [Test]
    public async Task AWordTakenByTheWorkerCanBeAskedForAgain()
    {
        var queue = new StudyEnrichmentQueue();
        queue.Enqueue(7);

        using var cancellation = new CancellationTokenSource();

        await foreach (var _ in queue.ReadAllAsync(cancellation.Token))
        {
            cancellation.Cancel();
            break;
        }

        queue.Enqueue(7);

        queue.PendingCount.Should().Be(1, "it may have been reopened since it was taken");
    }
}

/// <summary>
/// The mock client is what end-to-end runs exercise instead of a real model, so its study
/// content has to satisfy the same rules a real response is held to.
/// </summary>
public class MockGptClientStudyContentTests
{
    private static Word Word(string headword) => new()
    {
        Id = 1,
        Headword = headword,
        PartOfSpeech = "adjective",
        Language = Language.English
    };

    private static async Task<(WordStudyContent Content, StudyExampleSet Examples)> Generate(
        string headword, params string[] forms)
    {
        var prompt = StudyContentPrompt.For(
            Word(headword),
            StudyMaterialGaps.Meaning | StudyMaterialGaps.Examples | StudyMaterialGaps.Connections,
            forms);

        var response = await new MockGptClient().SendMessageAsync(prompt);

        var parsed = System.Text.Json.JsonSerializer.Deserialize<Reply>(
            response!, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var content = new WordStudyContent
        {
            WordId = 1,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = parsed.Definition,
            Usage = parsed.Usage,
            Etymology = parsed.Etymology,
            Cognates = parsed.Cognates,
            Mnemonic = parsed.Mnemonic,
            PromptVersion = StudyContentPrompt.Version
        };

        var examples = parsed.Examples!
            .Select((e, i) => new StudyExample { Id = i + 1, WordId = 1, Sentence = e.Sentence!, Form = e.Form!, Translation = e.Translation })
            .ToList();

        return (content, new StudyExampleSet(examples, forms));
    }

    [Test]
    public async Task TheMockProducesContentTheResolverAccepts()
    {
        var (content, examples) = await Generate("ubiquitous");

        var material = new StudyMaterialResolver().Resolve(Word("ubiquitous"), content, examples);

        material.HasMeaning.Should().BeTrue();
        material.HasContextSentence.Should().BeTrue("each sentence must contain the form it names to be usable");
        material.Connections.Should().NotBeNull();
        new StudyMaterialResolver().FindGaps(Word("ubiquitous"), content, examples)
            .Should().Be(StudyMaterialGaps.None);
    }

    [Test]
    public async Task TheGeneratedSentenceCanActuallyBeBlankedForACloze()
    {
        var (content, examples) = await Generate("ubiquitous");
        var material = new StudyMaterialResolver().Resolve(Word("ubiquitous"), content, examples);

        material.BlankedContextSentence.Should().Contain(HeadwordText.Blank).And.NotContain("ubiquitous");
    }

    [Test]
    public async Task TheMockGlossesEverySentenceItWritesAndEveryOneItIsAskedFor()
    {
        var word = new Word { Id = 1, Headword = "bonjour", PartOfSpeech = "interjection", Language = Language.French };

        var response = await new MockGptClient().SendMessageAsync(StudyContentPrompt.For(
            word, StudyMaterialGaps.Connections | StudyMaterialGaps.Examples,
            sentencesToGloss: new[] { "Il dit bonjour.", "Bonjour, Marie !" }));

        response.Should().Contain("\"word\":\"uses\",\"translation\":\"en:uses\"", "each example it writes is glossed");
        response.Should().Contain("\"word\":\"Marie\",\"translation\":\"en:Marie\"", "and each sentence it is asked to gloss");
        response.Should().NotContain("collocates");
    }

    [Test]
    public async Task TheMockWritesAnExampleForEachFormItIsAskedFor()
    {
        var (_, examples) = await Generate("prendre", "prend", "pris");

        examples.Examples.Select(e => e.Form).Should().Contain(new[] { "prend", "pris" });
        new StudyMaterialResolver().UncoveredForms(Word("prendre"), examples).Should().BeEmpty();
    }

    [Test]
    public async Task AWordMarkedToFailReturnsNothingSoTheFailurePathCanBeExercised()
    {
        var prompt = StudyContentPrompt.For(Word("zzfail-word"), StudyMaterialGaps.Meaning);

        (await new MockGptClient().SendMessageAsync(prompt)).Should().BeNull();
    }

    [Test]
    public async Task OrdinaryPromptsAreLeftToTheRecordedResponses()
    {
        var response = await new MockGptClient().SendMessageAsync("Some unrelated prompt about a word");

        response.Should().NotBeNull();
        response.Should().NotContain("\"sentence\"", "only study prompts get study content");
    }

    private record Reply(
        string? Definition,
        string? Usage,
        List<ReplyExample>? Examples,
        string? Etymology,
        string? Cognates,
        string? Mnemonic);

    private record ReplyExample(string? Sentence, string? Translation, string? Form, string? Collocation);
}
