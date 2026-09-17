using FluentAssertions;
using Moq;
using NUnit.Framework;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

public class ResolveVocabularyTermsTests
{
    private Mock<IVocabularyAnalyzer> _analyzer = null!;
    private ResolveVocabularyTermsQueryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _analyzer = new Mock<IVocabularyAnalyzer>();
        _handler = new ResolveVocabularyTermsQueryHandler(_analyzer.Object);
    }

    private Task<VocabularyTermResolution> Resolve(params string[] terms) =>
        _handler.Handle(
            new ResolveVocabularyTermsQuery { Terms = terms, Language = Language.French },
            CancellationToken.None);

    private void AnalyzerReturns(params AnalyzedTerm[] answers)
    {
        _analyzer
            .Setup(a => a.ResolveLemmasAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<Language>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answers);
    }

    /// <summary>
    /// The point of the rules: a list of plain nouns costs nothing to resolve.
    /// </summary>
    [Test]
    public async Task ShouldNotCallTheAnalyzerForTermsTheRulesSettle()
    {
        var result = await Resolve("une conférence", "la vente", "migrer");

        result.Resolved.Select(r => r.Lemma).Should().Equal("conférence", "vente", "migrer");
        result.Skipped.Should().BeEmpty();

        _analyzer.Verify(
            a => a.ResolveLemmasAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<Language>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ShouldNotCallTheAnalyzerForProse()
    {
        var result = await Resolve("Quel temps fait-il", "j'ai trente ans");

        result.Resolved.Should().BeEmpty();
        result.Skipped.Select(s => s.SourceTerm).Should().Equal("Quel temps fait-il", "j'ai trente ans");

        _analyzer.Verify(
            a => a.ResolveLemmasAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<Language>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task ShouldTakeTheLemmaTheAnalyzerReturns()
    {
        AnalyzerReturns(new AnalyzedTerm("vous allez", "aller", null));

        var result = await Resolve("Vous allez");

        result.Resolved.Should().ContainSingle();
        result.Resolved[0].SourceTerm.Should().Be("Vous allez", "the encounter names the term as written");
        result.Resolved[0].Lemma.Should().Be("aller");
    }

    /// <summary>
    /// A model answers with the article attached however firmly it is asked not to, and a
    /// headword carrying one would never match the word already in the vocabulary.
    /// </summary>
    [Test]
    public async Task ShouldStripAnArticleTheAnalyzerLeavesOn()
    {
        AnalyzerReturns(new AnalyzedTerm("cheveux noirs", "les cheveux", null));

        var result = await Resolve("les cheveux noirs");

        result.Resolved[0].Lemma.Should().Be("cheveux");
    }

    [Test]
    public async Task ShouldSkipWhatTheAnalyzerJudgesNotVocabulary()
    {
        AnalyzerReturns(new AnalyzedTerm("de rien", null, "Expression"));

        var result = await Resolve("de rien");

        result.Resolved.Should().BeEmpty();
        result.Skipped.Should().ContainSingle();
        result.Skipped[0].Reason.Should().Be("Expression");
    }

    [Test]
    public async Task ShouldSkipATermTheAnalyzerDoesNotAnswerFor()
    {
        AnalyzerReturns();

        var result = await Resolve("Vous allez");

        result.Resolved.Should().BeEmpty();
        result.Skipped.Should().ContainSingle().Which.SourceTerm.Should().Be("Vous allez");
    }

    /// <summary>
    /// Two forms of one word have to come back as one headword met twice: that is what
    /// turns the second row into another encounter instead of a second word.
    /// </summary>
    [Test]
    public async Task ShouldBringBothFormsOfANounToOneHeadword()
    {
        var result = await Resolve("une randonnée", "la randonnée");

        result.Resolved.Should().HaveCount(2);
        result.Resolved.Select(r => r.Lemma).Distinct().Should().ContainSingle().Which.Should().Be("randonnée");
        result.Resolved.Select(r => r.SourceTerm).Should().Equal("une randonnée", "la randonnée");
    }

    /// <summary>
    /// The same term twice is asked about once, but both rows still resolve - each one is
    /// an encounter.
    /// </summary>
    [Test]
    public async Task ShouldAskAboutARepeatedTermOnlyOnce()
    {
        AnalyzerReturns(new AnalyzedTerm("tu chantes", "chanter", null));

        var result = await Resolve("tu chantes", "Tu chantes");

        result.Resolved.Should().HaveCount(2);
        result.Resolved.Select(r => r.Lemma).Should().AllBe("chanter");

        _analyzer.Verify(
            a => a.ResolveLemmasAsync(It.Is<IReadOnlyList<string>>(t => t.Count == 1), It.IsAny<Language>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The rules encode French grammar; another language must not have them applied to it.
    /// </summary>
    [Test]
    public async Task ShouldPassAnotherLanguageStraightThrough()
    {
        var result = await _handler.Handle(
            new ResolveVocabularyTermsQuery
            {
                Terms = new[] { "Persist", "the sale" },
                Language = Language.English
            },
            CancellationToken.None);

        result.Resolved.Select(r => r.Lemma).Should().Equal("persist", "the sale");
        result.Skipped.Should().BeEmpty();
    }
}
