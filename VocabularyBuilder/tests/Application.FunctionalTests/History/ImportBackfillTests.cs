using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities.Imports;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// The statement the AddImportsAndHistory migration ran, against encounters written the way
/// imports wrote them before imports were recorded.
/// </summary>
/// <remarks>
/// The legacy data is made by upserting without an import id, which is exactly what every
/// import did before - so the encounters have the real identifier shapes, and no import items.
/// </remarks>
public class ImportBackfillTests : BaseTestFixture
{
    private static Task Encounter(
        string headword, WordEncounterSource source, string sourceIdentifier, string? context,
        Language language = Language.English) =>
        SendAsync(new UpsertWordCommand
        {
            Headword = headword,
            Language = language,
            Source = source,
            SourceIdentifier = sourceIdentifier,
            Context = context
        });

    private static async Task RunBackfill()
    {
        await WithServiceAsync<ApplicationDbContext, int>(context =>
            context.Database.ExecuteSqlRawAsync(ImportBackfill.Sql));
    }

    private static async Task<List<VocabularyImport>> Imports()
    {
        var imports = await ListAsync<VocabularyImport>();
        var items = await ListAsync<VocabularyImportItem>();

        foreach (var import in imports)
        {
            foreach (var item in items.Where(i => i.ImportId == import.Id))
            {
                import.Items.Add(item);
            }
        }

        return imports;
    }

    /// <summary>
    /// Book titles have colons of their own, so the import is found from the context and not
    /// from the first colon - which would have split this book into "Mistborn" imports.
    /// </summary>
    [Test]
    public async Task ShouldGroupAKindleBookWhoseTitleHasAColon()
    {
        const string book = "Mistborn: The Final Empire";

        await Encounter("ruddy", WordEncounterSource.KindleHighlights, $"{book}:ruddy:1", book);
        await Encounter("hovel", WordEncounterSource.KindleHighlights, $"{book}:hovel:8", book);

        await RunBackfill();

        var import = (await Imports()).Single();

        import.Kind.Should().Be(ImportKind.Kindle);
        import.Name.Should().Be(book);
        import.SourceIdentifierBase.Should().Be(book);
        import.IsReconstructed.Should().BeTrue();
        import.Status.Should().Be(ImportStatus.Completed);
        import.Items.Select(i => i.SourceTerm).Should().BeEquivalentTo("ruddy", "hovel");
        import.WordsCreated.Should().Be(2);
        import.WordsTouched.Should().Be(2);
    }

    [Test]
    public async Task ShouldSeparateImportsByTheirBase()
    {
        await Encounter("randonnée", WordEncounterSource.LingQ, "lesson 1:une randonnée", "lesson 1", Language.French);
        await Encounter("pied", WordEncounterSource.LingQ, "lingq:un pied", "LingQ import", Language.French);
        await Encounter("pas", WordEncounterSource.LessonNotes, "notes-ABCDEF0123456789:un pas", "Lesson notes", Language.French);

        await RunBackfill();

        var imports = await Imports();

        imports.Select(i => (i.Kind, i.Name, i.SourceIdentifierBase)).Should().BeEquivalentTo(new[]
        {
            (ImportKind.LingQ, (string?)"lesson 1", (string?)"lesson 1"),
            (ImportKind.LingQ, null, "lingq"),
            (ImportKind.LessonNotes, null, "notes-ABCDEF0123456789")
        });

        imports.Single(i => i.Name == "lesson 1").Items.Single().SourceTerm.Should().Be("une randonnée");
        imports.Should().OnlyContain(i => i.Language == Language.French);
    }

    /// <summary>
    /// A word first met in one import and again in another is new in the first only.
    /// </summary>
    [Test]
    public async Task ShouldCountAWordAsCreatedOnlyByTheImportThatFirstMetIt()
    {
        await Encounter("derelict", WordEncounterSource.OxfordDictionaryList, "LISTONE:derelict", "Text Word Import");
        await Encounter("derelict", WordEncounterSource.OxfordDictionaryList, "LISTTWO:derelict", "Text Word Import");

        await RunBackfill();

        var imports = await Imports();

        imports.Single(i => i.SourceIdentifierBase == "LISTONE").Items.Single().Outcome.Should().Be(ImportItemOutcome.Created);
        imports.Single(i => i.SourceIdentifierBase == "LISTTWO").Items.Single().Outcome.Should().Be(ImportItemOutcome.Existing);
        imports.Single(i => i.SourceIdentifierBase == "LISTTWO").WordsCreated.Should().Be(0);
    }

    [Test]
    public async Task ShouldLeaveManualEncountersOut()
    {
        await SendAsync(new CreateWordCommand { Headword = "tree", Language = Language.English });
        await Encounter("house", WordEncounterSource.Manual, "2026-09-01", null);

        await RunBackfill();

        (await Imports()).Should().BeEmpty();
    }
}
