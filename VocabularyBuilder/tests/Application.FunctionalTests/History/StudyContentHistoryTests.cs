using System.Text.Json;
using VocabularyBuilder.Application.History.Queries;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Imports.Commands;
using VocabularyBuilder.Application.Imports.Queries;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// Study content is the largest thing the model is asked for - every word, once per prompt
/// version - and these hold what that leaves in the history: what each generation wrote, what
/// it replaced, and which import caused a later one.
/// </summary>
public class StudyContentHistoryTests : BaseTestFixture
{
    private static async Task<Word> GivenWord(string headword = "parler")
    {
        var word = new Word { Headword = headword, Language = Language.French };
        await AddAsync(word);
        return word;
    }

    private static async Task<WordStudyContent> ContentOf(int wordId) =>
        (await ListAsync<WordStudyContent>()).Single(c => c.WordId == wordId);

    /// <summary>Brings the word in through an import, met in the given form.</summary>
    private static async Task<int> ImportForm(string headword, string form, string? sentence = null)
    {
        var importId = await SendAsync(new StartImportCommand
        {
            Kind = ImportKind.LingQ,
            Language = Language.French,
            Name = "lesson",
            SourceIdentifierBase = "lesson"
        });

        await SendAsync(new SaveVocabularyTermsCommand
        {
            Terms = new[] { new ImportedTerm(form, headword, Form: form, Sentence: sentence) },
            Language = Language.French,
            Source = WordEncounterSource.LingQ,
            SourceIdentifierBase = "lesson",
            ImportId = importId
        });

        await SendAsync(new CompleteImportCommand(importId, 1));

        return importId;
    }

    [Test]
    public async Task ShouldRecordWhatAGenerationWrote()
    {
        var word = await GivenWord();

        (await SendAsync(new EnrichWordStudyContentCommand(word.Id))).Should().Be(EnrichmentOutcome.Generated);

        var entry = (await ListAsync<ActivityLogEntry>()).Single(e => e.Action == ActivityAction.StudyContentGenerated);

        entry.WordId.Should().Be(word.Id);
        entry.Summary.Should().StartWith(StudyContentPrompt.Version + ":").And.Contain("example");

        using var details = JsonDocument.Parse(entry.Details!);
        details.RootElement.GetProperty("promptVersion").GetString().Should().Be(StudyContentPrompt.Version);
        details.RootElement.GetProperty("examplesAdded").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ShouldStampTheStudyContentCallWithItsPromptVersion()
    {
        var word = await GivenWord();

        await SendAsync(new EnrichWordStudyContentCommand(word.Id));

        var call = (await ListAsync<ExternalCallLog>()).Single(c => c.Purpose == ExternalCallPurpose.StudyContent);

        call.PromptVersion.Should().Be(StudyContentPrompt.Version);
        call.WordId.Should().Be(word.Id);
    }

    /// <summary>
    /// A generation for a new prompt version replaces the word's etymology and mnemonic. The
    /// activity log is the only place the old ones survive.
    /// </summary>
    [Test]
    public async Task ShouldKeepWhatARegenerationReplaced()
    {
        var word = await GivenWord();
        await AddAsync(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = "to speak",
            Etymology = "An old etymology",
            PromptVersion = "v0"
        });

        await SendAsync(new EnrichWordStudyContentCommand(word.Id));

        var entry = (await ListAsync<ActivityLogEntry>()).Single(e => e.Action == ActivityAction.StudyContentGenerated);

        using var details = JsonDocument.Parse(entry.Details!);
        var changes = details.RootElement.GetProperty("changes");
        changes.GetProperty("Etymology").GetProperty("from").GetString().Should().Be("An old etymology");
        changes.GetProperty("PromptVersion").GetProperty("from").GetString().Should().Be("v0");
    }

    /// <summary>
    /// An import that brings a known word in a new form reopens its finished content - and
    /// the model call that follows, a study session later, is filed under that import.
    /// </summary>
    [Test]
    public async Task ShouldTieTheLaterCallANewFormCausesBackToItsImport()
    {
        var word = await GivenWord();
        await SendAsync(new EnrichWordStudyContentCommand(word.Id));
        (await ContentOf(word.Id)).Status.Should().Be(StudyContentStatus.Ready);

        var importId = await ImportForm("parler", "parlons");

        var reopened = (await ListAsync<ActivityLogEntry>()).Single(e => e.Action == ActivityAction.StudyContentReopened);
        reopened.ImportId.Should().Be(importId);
        reopened.WordId.Should().Be(word.Id);
        (await ContentOf(word.Id)).ReopenedByImportId.Should().Be(importId);

        var item = (await ListAsync<Domain.Entities.Imports.VocabularyImportItem>()).Single(i => i.ImportId == importId);
        item.Form.Should().Be("parlons");
        item.ContentReopened.Should().BeTrue();

        await SendAsync(new EnrichWordStudyContentCommand(word.Id));

        var later = (await ListAsync<ExternalCallLog>())
            .Where(c => c.Purpose == ExternalCallPurpose.StudyContent)
            .OrderBy(c => c.Id)
            .Last();
        later.ImportId.Should().Be(importId);

        (await ContentOf(word.Id)).ReopenedByImportId.Should().BeNull("the content is ready again");

        var details = await SendAsync(new GetImportDetailsQuery(importId));
        details!.ContentReopened.Should().Be(1);
        details.ExternalCalls.LaterStudyContentCalls.Should().Be(1);
    }

    [Test]
    public async Task ShouldRecordTheSentenceAnImportKeptAsAnExample()
    {
        await GivenWord();

        var importId = await ImportForm("parler", "parlons", sentence: "Nous parlons français.");

        var details = await SendAsync(new GetImportDetailsQuery(importId));
        var item = details!.Items.Single();

        item.Form.Should().Be("parlons");
        item.ExampleAdded.Should().BeTrue();
        item.ContentReopened.Should().BeFalse("the sentence covers the form, so nothing has to be asked for");
        details.ExamplesAdded.Should().Be(1);
    }

    [Test]
    public async Task ShouldTotalCallsByPurposeAndPromptVersion()
    {
        var word = await GivenWord();
        await SendAsync(new EnrichWordStudyContentCommand(word.Id));

        var usage = await SendAsync(new GetCallUsageQuery());

        var studyContent = usage.Single(u => u.Purpose == "StudyContent");
        studyContent.PromptVersion.Should().Be(StudyContentPrompt.Version);
        studyContent.Calls.Should().Be(1);
        usage.Should().Contain(u => u.Purpose == "DictionaryEntry", "the word was filled from the dictionary first");
    }
}
