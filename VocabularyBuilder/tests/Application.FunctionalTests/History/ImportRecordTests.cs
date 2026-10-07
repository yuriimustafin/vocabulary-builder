using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Imports.Queries;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Entities.Imports;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// What an import leaves behind about itself: the run, and every term it was given.
/// </summary>
public class ImportRecordTests : BaseTestFixture
{
    private const string Header =
        "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2";

    private static ImportLingQWordsCommand LingQ(string rows, string? listName = null, string? tag = null) =>
        new()
        {
            FileContent = Header + "\n" + rows,
            Language = Language.French,
            ListName = listName,
            Tag = tag,
            FileName = "lingq-export.csv"
        };

    [Test]
    public async Task ShouldRecordTheImportWithItsNameFileAndTags()
    {
        var result = await SendAsync(LingQ("une main,,,,,,,,,\n", listName: "lesson 12", tag: "preply"));

        result.ImportId.Should().NotBeNull();

        var import = (await ListAsync<VocabularyImport>()).Single();

        import.Id.Should().Be(result.ImportId);
        import.Kind.Should().Be(ImportKind.LingQ);
        import.Language.Should().Be(Language.French);
        import.Name.Should().Be("lesson 12");
        import.FileName.Should().Be("lingq-export.csv");
        import.Tags.Should().Equal("preply");
        import.Status.Should().Be(ImportStatus.Completed);
        import.CompletedAtUtc.Should().NotBeNull();
        import.IsReconstructed.Should().BeFalse();
    }

    [Test]
    public async Task ShouldListEveryTermWithTheWordItBecame()
    {
        var result = await SendAsync(LingQ("une randonnée,,,,,,,,,\nun pied,,,,,,,,,\n"));

        var details = await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value));

        details!.Items.Select(i => (i.SourceTerm, i.Headword, i.Outcome)).Should().Equal(
            ("une randonnée", "randonnée", "Created"),
            ("un pied", "pied", "Created"));
        details.Items.Should().OnlyContain(i => i.WordId != null && i.EncounterAdded);
        details.WordsCreated.Should().Be(2);
        details.WordsTouched.Should().Be(2);
        details.TermsRead.Should().Be(2);
    }

    /// <summary>
    /// Two forms of one word in one file: the second lands on a word only this import made, and
    /// is not "already known" for it.
    /// </summary>
    [Test]
    public async Task ShouldCountAWordTwoTermsLandOnAsCreatedByTheImport()
    {
        var result = await SendAsync(LingQ("une randonnée,,,,,,,,,\nla randonnée,,,,,,,,,\n"));

        var details = await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value));

        details!.Items.Should().HaveCount(2);
        details.Items.Should().OnlyContain(i => i.Outcome == "Created" && i.Headword == "randonnée");
        details.WordsCreated.Should().Be(1);
        details.WordsTouched.Should().Be(1);
    }

    /// <summary>
    /// A word shows in every import that touched it, so a second import lists it too - as
    /// already known, and without an encounter when it was the same term met again.
    /// </summary>
    [Test]
    public async Task ShouldShowAWordInALaterImportAsAlreadyKnown()
    {
        await SendAsync(LingQ("une main,,,,,,,,,\n", listName: "first"));
        var second = await SendAsync(LingQ("une main,,,,,,,,,\nun pied,,,,,,,,,\n", listName: "first"));

        var details = await SendAsync(new GetImportDetailsQuery(second.ImportId!.Value));

        var main = details!.Items.Single(i => i.Headword == "main");
        main.Outcome.Should().Be("Existing");
        main.EncounterAdded.Should().BeFalse();

        details.Items.Single(i => i.Headword == "pied").Outcome.Should().Be("Created");
        details.WordsCreated.Should().Be(1);
        details.WordsTouched.Should().Be(2);
        details.EncountersCreated.Should().Be(1);

        (await ListAsync<VocabularyImport>()).Should().HaveCount(2);
    }

    [Test]
    public async Task ShouldKeepTheTermsItSetAsideWithTheReason()
    {
        var result = await SendAsync(LingQ("une main,,,,,,,,,\nComment ça va ?,,,,,,,,,\n"));

        var details = await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value));

        var skipped = details!.Items.Single(i => i.Outcome == "Skipped");
        skipped.SourceTerm.Should().Be("Comment ça va ?");
        skipped.Reason.Should().Be("Question");
        skipped.WordId.Should().BeNull();
        details.TermsSkipped.Should().Be(1);
        details.TermsRead.Should().Be(2);
    }

    /// <summary>The import brought the word in whether or not it is still there.</summary>
    [Test]
    public async Task ShouldKeepADeletedWordInTheImportByItsHeadword()
    {
        var result = await SendAsync(LingQ("une main,,,,,,,,,\n"));
        var word = (await ListAsync<Word>()).Single();

        await SendAsync(new DeleteWordCommand(word.Id));

        var details = await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value));

        var item = details!.Items.Single();
        item.WordId.Should().BeNull();
        item.Headword.Should().Be("main");
        item.CurrentStatus.Should().BeNull();
    }

    [Test]
    public async Task ShouldRecordABulkImportLookedUpAsItRan()
    {
        var result = await SendAsync(new ImportWordsFromDictionaryCommand
        {
            Words = new List<string> { "maison" },
            Language = Language.French,
            SourceType = DictionarySourceType.Gpt,
            ParseImmediately = true,
            ListName = "bulk list"
        });

        var details = await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value));

        details!.Kind.Should().Be("BulkList");
        details.Name.Should().Be("bulk list");
        details.Items.Should().ContainSingle(i => i.Headword == "maison" && i.Outcome == "Created");
        details.ExternalCalls.Calls.Should().BeGreaterThan(0, "the lookup ran inside the import");
    }

    [Test]
    public async Task ShouldLogTheImportInTheActivityLog()
    {
        var result = await SendAsync(LingQ("une main,,,,,,,,,\n", listName: "lesson 12"));

        var entry = (await ListAsync<ActivityLogEntry>()).Single(e => e.Action == ActivityAction.ImportCompleted);

        entry.ImportId.Should().Be(result.ImportId);
        entry.Summary.Should().Contain("lesson 12").And.Contain("1 new");
    }

    [Test]
    public async Task ShouldListImportsNewestFirstForTheLanguageOnly()
    {
        await SendAsync(LingQ("une main,,,,,,,,,\n", listName: "older"));
        await SendAsync(LingQ("un pied,,,,,,,,,\n", listName: "newer"));
        await SendAsync(new ImportWordsFromDictionaryCommand
        {
            Words = new List<string> { "house" },
            Language = Language.English,
            ListName = "english"
        });

        var page = await SendAsync(new GetImportsQuery(Language.French));

        page.Items.Select(i => i.Name).Should().Equal("newer", "older");
    }

    [Test]
    public async Task ShouldNotShowOneUsersImportsToAnother()
    {
        var result = await SendAsync(LingQ("une main,,,,,,,,,\n"));

        await RunAsUserAsync("someone-else@local", "Testing1234!", Array.Empty<string>());

        (await SendAsync(new GetImportsQuery(Language.French))).Items.Should().BeEmpty();
        (await SendAsync(new GetImportDetailsQuery(result.ImportId!.Value))).Should().BeNull();
    }
}
