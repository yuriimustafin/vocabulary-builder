using FluentAssertions;
using MediatR;
using Moq;
using NUnit.Framework;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

public class ImportLessonNotesTests
{
    private Mock<ISender> _sender = null!;
    private Mock<IVocabularyAnalyzer> _analyzer = null!;
    private ImportLessonNotesCommandHandler _handler = null!;
    private SaveVocabularyTermsCommand? _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = null;
        _sender = new Mock<ISender>();
        _analyzer = new Mock<IVocabularyAnalyzer>();
        _handler = new ImportLessonNotesCommandHandler(_analyzer.Object, _sender.Object);

        _sender
            .Setup(s => s.Send(It.IsAny<SaveVocabularyTermsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((command, _) => _saved = (SaveVocabularyTermsCommand)command)
            .ReturnsAsync(new SaveVocabularyTermsResult());

        _sender
            .Setup(s => s.Send(It.IsAny<ResolveVocabularyTermsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object query, CancellationToken _) => new VocabularyTermResolution
            {
                Resolved = ((ResolveVocabularyTermsQuery)query).Terms
                    .Select(t => new ResolvedTerm(t, t))
                    .ToList()
            });
    }

    private void Extracts(params string[] items)
    {
        _analyzer
            .Setup(a => a.ExtractItemsAsync(It.IsAny<string>(), It.IsAny<Language>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
    }

    private Task<VocabularyImportResult> Import(string notes, string? listName = null) =>
        _handler.Handle(
            new ImportLessonNotesCommand { Notes = notes, Language = Language.French, ListName = listName },
            CancellationToken.None);

    [Test]
    public async Task ShouldImportTheItemsReadOutOfTheNotes()
    {
        Extracts("un point de vue", "migrer", "une oeuvre d'art");

        var result = await Import("un point de vue=viewpoint\nmigrer=to migrate\nune oeuvre d'art=masterpiece");

        result.TermsRead.Should().Be(3);
        result.TermsImported.Should().Be(3);
        _saved!.Terms.Select(t => t.SourceTerm).Should().Equal("un point de vue", "migrer", "une oeuvre d'art");
    }

    [Test]
    public async Task ShouldPassTheWholeNotesToTheAnalyzer()
    {
        Extracts("un pas");
        const string notes = "un pas=step\nun serpent=snake";

        await Import(notes);

        _analyzer.Verify(
            a => a.ExtractItemsAsync(notes, Language.French, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ShouldRecordTheEncountersAgainstLessonNotes()
    {
        Extracts("un pas");

        await Import("un pas=step");

        _saved!.Source.Should().Be(WordEncounterSource.LessonNotes);
        _saved.Context.Should().Be("Lesson notes");
    }

    [Test]
    public async Task ShouldNameTheImportAfterTheLessonWhenOneIsGiven()
    {
        Extracts("un pas");

        await Import("un pas=step", listName: "Lesson 12");

        _saved!.SourceIdentifierBase.Should().Be("Lesson 12");
        _saved.Context.Should().Be("Lesson 12");
    }

    /// <summary>
    /// Pasting the same notes twice must not count as meeting each word twice.
    /// </summary>
    [Test]
    public async Task ShouldIdentifyUnnamedNotesByTheirContent()
    {
        Extracts("un pas");

        await Import("un pas=step");
        var first = _saved!.SourceIdentifierBase;

        await Import("un pas=step");
        var again = _saved!.SourceIdentifierBase;

        await Import("un pas=step\nun serpent=snake");
        var extended = _saved!.SourceIdentifierBase;

        again.Should().Be(first);
        extended.Should().NotBe(first);
    }

    [Test]
    public async Task ShouldSaveNothingWhenTheNotesHoldNoVocabulary()
    {
        Extracts();

        var result = await Import("Lesson 4\n---");

        result.TermsRead.Should().Be(0);
        result.TermsImported.Should().Be(0);
        _saved.Should().BeNull();
    }

    [Test]
    public async Task ShouldCarryNoNotesOntoTheEncounter()
    {
        Extracts("un pas");

        await Import("un pas=step");

        _saved!.Terms.Single().Notes.Should().BeNull(
            "the translation in the notes is not the word's content");
    }
}
