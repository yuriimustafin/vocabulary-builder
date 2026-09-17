using FluentAssertions;
using MediatR;
using Moq;
using NUnit.Framework;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

public class ImportLingQWordsTests
{
    private const string Header =
        "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2";

    private Mock<ISender> _sender = null!;
    private ImportLingQWordsCommandHandler _handler = null!;
    private SaveVocabularyTermsCommand? _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = null;
        _sender = new Mock<ISender>();
        _handler = new ImportLingQWordsCommandHandler(_sender.Object);

        _sender
            .Setup(s => s.Send(It.IsAny<SaveVocabularyTermsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((command, _) => _saved = (SaveVocabularyTermsCommand)command)
            .ReturnsAsync(new SaveVocabularyTermsResult());
    }

    /// <summary>Resolves every term to itself, so the test is about the import, not the rules.</summary>
    private void ResolvesEverything()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<ResolveVocabularyTermsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object query, CancellationToken _) => new VocabularyTermResolution
            {
                Resolved = ((ResolveVocabularyTermsQuery)query).Terms
                    .Select(t => new ResolvedTerm(t, t))
                    .ToList()
            });
    }

    private void Resolves(VocabularyTermResolution resolution)
    {
        _sender
            .Setup(s => s.Send(It.IsAny<ResolveVocabularyTermsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolution);
    }

    private Task<VocabularyImportResult> Import(string csv, string? listName = null) =>
        _handler.Handle(
            new ImportLingQWordsCommand { FileContent = csv, Language = Language.French, ListName = listName },
            CancellationToken.None);

    [Test]
    public async Task ShouldSendEveryTermInTheExportForResolution()
    {
        ResolvesEverything();

        var result = await Import(Header + "\nune conférence,,preply,,,,en,conference,,\nla vente,,,,,,,,,\n");

        result.TermsRead.Should().Be(2);
        result.TermsImported.Should().Be(2);
        _saved!.Terms.Select(t => t.Lemma).Should().Equal("une conférence", "la vente");
    }

    /// <summary>
    /// The learner's own translation is not carried over - an imported word takes its
    /// content from the dictionary, so only the sentence rides along as context.
    /// </summary>
    [Test]
    public async Task ShouldKeepTheSentenceAndNotTheTranslation()
    {
        ResolvesEverything();

        await Import(Header + "\nune conférence,J'écoute une conférence.,,,,,en,conference,,\n");

        var term = _saved!.Terms.Single();
        term.Notes.Should().Be("J'écoute une conférence.");
        term.Notes.Should().NotContain("conference", "the meaning column is the learner's own translation");
    }

    [Test]
    public async Task ShouldReportTermsThatWereNotVocabulary()
    {
        Resolves(new VocabularyTermResolution
        {
            Resolved = { new ResolvedTerm("une conférence", "conférence") },
            Skipped = { new SkippedTerm("Quel temps fait-il", "Question") }
        });

        var result = await Import(
            Header + "\nune conférence,,,,,,,,,\nQuel temps fait-il,,,,,,,,,\n");

        result.TermsRead.Should().Be(2);
        result.TermsImported.Should().Be(1);
        result.Skipped.Should().ContainSingle();
        result.Skipped[0].SourceTerm.Should().Be("Quel temps fait-il");
        result.Skipped[0].Reason.Should().Be("Question");
    }

    [Test]
    public async Task ShouldRecordTheEncountersAgainstLingQ()
    {
        ResolvesEverything();

        await Import(Header + "\nune main,,,,,,,,,\n");

        _saved!.Source.Should().Be(WordEncounterSource.LingQ);
        _saved.Language.Should().Be(Language.French);
    }

    [Test]
    public async Task ShouldNameTheImportAfterTheListWhenOneIsGiven()
    {
        ResolvesEverything();

        await Import(Header + "\nune main,,,,,,,,,\n", listName: "Preply week 3");

        _saved!.SourceIdentifierBase.Should().Be("Preply week 3");
        _saved.Context.Should().Be("Preply week 3");
    }

    /// <summary>
    /// Without a name the file identifies itself, so re-importing the same export adds no
    /// encounters while a later export that has grown adds only its new rows.
    /// </summary>
    [Test]
    public async Task ShouldIdentifyAnUnnamedImportByItsContent()
    {
        ResolvesEverything();

        await Import(Header + "\nune main,,,,,,,,,\n");
        var first = _saved!.SourceIdentifierBase;

        await Import(Header + "\nune main,,,,,,,,,\n");
        var again = _saved!.SourceIdentifierBase;

        await Import(Header + "\nune main,,,,,,,,,\nun pied,,,,,,,,,\n");
        var grown = _saved!.SourceIdentifierBase;

        again.Should().Be(first);
        grown.Should().NotBe(first);
    }

    [Test]
    public async Task ShouldSaveNothingForAnEmptyExport()
    {
        ResolvesEverything();

        var result = await Import(Header + "\n");

        result.TermsRead.Should().Be(0);
        result.TermsImported.Should().Be(0);
        _saved.Should().BeNull();
    }
}
