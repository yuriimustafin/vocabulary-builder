using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study.Queries;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.Study;

using static Testing;

/// <summary>
/// What the study queue does with words whose ordering does not single one out.
/// </summary>
/// <remarks>
/// These run against the real host, which is the whole point: the context is configured with
/// <c>QuerySplittingBehavior.SplitQuery</c>, so an <c>Include</c> is a second query that
/// re-evaluates the parent - ties and all. A hand-built context would not reproduce it.
/// </remarks>
public class StudyQueueSelectionTests : BaseTestFixture
{
    /// <summary>A candidate for introduction: no review card, one encounter, a frequency.</summary>
    private static async Task<Word> GivenCandidate(string headword, int frequency, string? definition)
    {
        var word = new Word
        {
            Headword = headword,
            Language = Language.French,
            Frequency = frequency,
            PartOfSpeech = "noun",
            Gender = GrammaticalGender.Feminine
        };

        if (definition is not null)
        {
            word.Senses = new List<Sense>
            {
                new() { Definition = definition, Examples = new List<string>() }
            };
        }

        word.WordEncounters = new List<WordEncounter>
        {
            new() { Source = WordEncounterSource.LingQ, SourceIdentifier = $"test:{headword}" }
        };

        await AddAsync(word);

        return word;
    }

    /// <summary>
    /// The production failure, reduced: two words tied on every key the selector orders by.
    ///
    /// The word list and each Include are separate statements under split queries, and SQLite
    /// is free to break the tie differently in each. When it does, the word that was
    /// materialised comes back holding another word's senses - or none - and a fully filled-in
    /// word looks exactly like one the dictionary never reached: it cannot be rendered, so it
    /// is reported as "preparing" and queued for enrichment, which finds nothing missing and
    /// writes nothing. Every session afterwards repeats it.
    /// </summary>
    [Test]
    public async Task ShouldKeepItsOwnSensesWhenCandidatesTieOnEverySortKey()
    {
        // Same frequency and the same number of encounters: nothing but the id separates them
        var first = await GivenCandidate("manuel", 663, "manual, handbook");
        var second = await GivenCandidate("frire", 663, "to fry");

        var selected = await WithServiceAsync<IApplicationDbContext, List<Word>>(
            context => NewWordSelector.SelectAsync(context, Language.French, 1, CancellationToken.None));

        selected.Should().ContainSingle();

        var word = selected.Single();
        word.Id.Should().BeOneOf(first.Id, second.Id);
        word.Senses.Should().NotBeNullOrEmpty(
            "a word must come back with its own senses, whichever way the tie went");
        word.Senses!.Single().Definition.Should().Be(
            word.Id == first.Id ? "manual, handbook" : "to fry");
    }

    /// <summary>
    /// A filled-in word is studiable straight away, without a round trip through enrichment.
    /// </summary>
    [Test]
    public async Task ShouldRenderATiedCandidateRatherThanReportItAsPreparing()
    {
        await GivenCandidate("manuel", 663, "manual, handbook");
        await GivenCandidate("frire", 663, "to fry");

        var queue = await SendAsync(new GetStudyQueueQuery(Language.French));

        queue.PendingEnrichmentCount.Should().Be(0);
        queue.Cards.Should().HaveCount(2);
        queue.Cards.Select(c => c.Exercise.Answer).Should().NotContainNulls();
    }

    /// <summary>
    /// Enrichment gives up after a few failures. A word it has given up on must stop being
    /// counted as waiting: the session's spinner has no automatic retry behind it, so a word
    /// that will never render would leave "Preparing 1 word" on screen for good.
    /// </summary>
    [Test]
    public async Task ShouldNotReportAWordEnrichmentHasGivenUpOnAsPreparing()
    {
        var word = await GivenCandidate("insoluble", 700, definition: null);

        await AddAsync(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Failed,
            GenerationAttempts = 3,
            LastError = "No usable definition was produced."
        });

        var queue = await SendAsync(new GetStudyQueueQuery(Language.French));

        queue.Cards.Should().BeEmpty("the word has no meaning, so nothing can be built from it");
        queue.PendingEnrichmentCount.Should().Be(0, "nothing is going to change it");
    }

    /// <summary>
    /// The counterpart: a word enrichment has not finished with is still worth waiting for.
    /// </summary>
    [Test]
    public async Task ShouldStillReportAWordEnrichmentMayYetFillAsPreparing()
    {
        await GivenCandidate("insoluble", 700, definition: null);

        var queue = await SendAsync(new GetStudyQueueQuery(Language.French));

        queue.Cards.Should().BeEmpty();
        queue.PendingEnrichmentCount.Should().Be(1);
    }
}
