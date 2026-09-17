using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Domain.UnitTests.Helpers;

public class FrenchTermNormalizerTests
{
    [TestCase("une conférence", "conférence")]
    [TestCase("la vente", "vente")]
    [TestCase("le journal", "journal")]
    [TestCase("les cheveux", "cheveux")]
    [TestCase("un audio", "audio")]
    [TestCase("des lunettes", "lunettes")]
    [TestCase("du thé", "thé")]
    [TestCase("l'Union", "union")]
    [TestCase("de la crème", "crème")]
    public void ShouldReadTheHeadwordBehindAnArticle(string term, string lemma)
    {
        var analysis = FrenchTermNormalizer.Analyse(term);

        analysis.Verdict.Should().Be(TermVerdict.Lemma);
        analysis.Lemma.Should().Be(lemma);
        analysis.SourceTerm.Should().Be(term, "the encounter has to name the term as it was written");
    }

    [TestCase("migrer")]
    [TestCase("également")]
    [TestCase("similaire")]
    [TestCase("aujourd'hui")]
    public void ShouldTakeABareWordAsItsOwnHeadword(string term)
    {
        var analysis = FrenchTermNormalizer.Analyse(term);

        analysis.Verdict.Should().Be(TermVerdict.Lemma);
        analysis.Lemma.Should().Be(term.ToLowerInvariant());
    }

    /// <summary>
    /// Both forms of one word have to reach the same headword - that is what lets the
    /// second one count as another encounter rather than a second word.
    /// </summary>
    [Test]
    public void ShouldReduceBothArticlesOfANounToOneHeadword()
    {
        FrenchTermNormalizer.Analyse("une randonnée").Lemma
            .Should().Be(FrenchTermNormalizer.Analyse("la randonnée").Lemma);
    }

    [TestCase("Vous allez")]
    [TestCase("tu chantes")]
    [TestCase("Elle est")]
    [TestCase("nous finissons")]
    [TestCase("elles dansent")]
    [TestCase("je pars")]
    public void ShouldSendAConjugatedVerbForAnalysis(string term)
    {
        var analysis = FrenchTermNormalizer.Analyse(term);

        analysis.Verdict.Should().Be(TermVerdict.NeedsAnalysis);
        analysis.Candidate.Should().Be(term.ToLowerInvariant(), "the model needs the pronoun to read the person");
    }

    [TestCase("les cheveux noirs", "cheveux noirs")]
    [TestCase("une pomme de terre", "pomme de terre")]
    [TestCase("un sac à dos", "sac à dos")]
    [TestCase("la salle de classe", "salle de classe")]
    [TestCase("le tir à l'arc", "tir à l'arc")]
    [TestCase("mettre en scène", "mettre en scène")]
    public void ShouldSendACompoundForAnalysisWithoutItsArticle(string term, string candidate)
    {
        var analysis = FrenchTermNormalizer.Analyse(term);

        analysis.Verdict.Should().Be(TermVerdict.NeedsAnalysis);
        analysis.Candidate.Should().Be(candidate);
    }

    [TestCase("Quel temps fait-il")]
    [TestCase("Quelle heure est-il")]
    [TestCase("Comment allez-vous")]
    [TestCase("Qu'est-ce que ça veut dire")]
    [TestCase("Où est")]
    [TestCase("Pouvez-vous me dire où est la bibliothèque")]
    [TestCase("Est-ce que tu as gagné au bowling")]
    public void ShouldSetAsideAQuestionWithoutAskingTheModel(string term)
    {
        FrenchTermNormalizer.Analyse(term).Verdict.Should().Be(TermVerdict.NotVocabulary);
    }

    [TestCase("il fait beau")]
    [TestCase("il y a du soleil")]
    [TestCase("c'est venteux")]
    [TestCase("j'ai trente ans")]
    [TestCase("je parle ukrainien et anglais")]
    [TestCase("nous avons joué")]
    public void ShouldSetAsideAClause(string term)
    {
        FrenchTermNormalizer.Analyse(term).Verdict.Should().Be(TermVerdict.NotVocabulary);
    }

    [Test]
    public void ShouldSetAsideProseHoweverItIsBuilt()
    {
        var analysis = FrenchTermNormalizer.Analyse("Les clés sont dans une boîte sécurisée");

        analysis.Verdict.Should().Be(TermVerdict.NotVocabulary);
        analysis.Reason.Should().Be("Sentence");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void ShouldSetAsideAnEmptyTerm(string? term)
    {
        FrenchTermNormalizer.Analyse(term).Verdict.Should().Be(TermVerdict.NotVocabulary);
    }

    /// <summary>
    /// A typographic apostrophe is what a copied note actually contains, and it must not
    /// make "l'arbre" a different word from "l'arbre".
    /// </summary>
    [Test]
    public void ShouldTreatBothApostrophesAlike()
    {
        FrenchTermNormalizer.Analyse("une oeuvre d’art").Candidate
            .Should().Be(FrenchTermNormalizer.Analyse("une oeuvre d'art").Candidate);
    }

    [TestCase("le chat", "chat")]
    [TestCase("une maison", "maison")]
    [TestCase("chat", "chat")]
    [TestCase("pomme de terre", "pomme de terre")]
    public void ShouldStripAnArticleFromAModelsAnswer(string answer, string expected)
    {
        FrenchTermNormalizer.StripArticle(answer).Should().Be(expected);
    }

    /// <summary>
    /// Only a one-letter prefix elides. A word that merely contains an apostrophe has to
    /// survive whole, or "aujourd'hui" would be filed under "hui".
    /// </summary>
    [Test]
    public void ShouldNotMistakeAnInternalApostropheForAnElision()
    {
        FrenchTermNormalizer.Analyse("aujourd'hui").Lemma.Should().Be("aujourd'hui");
    }
}
