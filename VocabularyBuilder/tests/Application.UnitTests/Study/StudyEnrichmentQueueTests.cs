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
    public void AskingTwiceForTheSameWordIsHarmless()
    {
        // The enrichment command is idempotent, so a duplicate request costs a lookup
        // rather than a second generation.
        var queue = new StudyEnrichmentQueue();

        queue.Enqueue(7);
        queue.Enqueue(7);

        queue.PendingCount.Should().Be(2);
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

    private static async Task<WordStudyContent> Generate(string headword)
    {
        var prompt = StudyContentPrompt.For(
            Word(headword), StudyMaterialGaps.Meaning | StudyMaterialGaps.ContextSentence);

        var response = await new MockGptClient().SendMessageAsync(prompt);

        var parsed = System.Text.Json.JsonSerializer.Deserialize<Reply>(
            response!, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        return new WordStudyContent
        {
            WordId = 1,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = parsed.Definition,
            GeneratedContextSentence = parsed.Sentence
        };
    }

    [Test]
    public async Task TheMockProducesContentTheResolverAccepts()
    {
        var content = await Generate("ubiquitous");

        var material = new StudyMaterialResolver().Resolve(Word("ubiquitous"), content);

        material.HasMeaning.Should().BeTrue();
        material.HasContextSentence.Should().BeTrue("the sentence must contain the headword to be usable");
    }

    [Test]
    public async Task TheGeneratedSentenceCanActuallyBeBlankedForACloze()
    {
        var content = await Generate("ubiquitous");
        var material = new StudyMaterialResolver().Resolve(Word("ubiquitous"), content);

        HeadwordText.Blankify(material.ContextSentence!, "ubiquitous")
            .Should().Contain(HeadwordText.Blank).And.NotContain("ubiquitous");
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

    private record Reply(string? Definition, string? Sentence);
}
