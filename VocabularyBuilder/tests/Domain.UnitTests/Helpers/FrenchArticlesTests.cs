using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Domain.UnitTests.Helpers;

public class FrenchArticlesTests
{
    [TestCase("maison", GrammaticalGender.Feminine, "la", "une")]
    [TestCase("livre", GrammaticalGender.Masculine, "le", "un")]
    [TestCase("dentiste", GrammaticalGender.Common, "le/la", "un/une")]
    public void ShouldPickTheArticleByGender(
        string headword, GrammaticalGender gender, string definite, string indefinite)
    {
        var article = FrenchArticles.For(headword, gender, isPluralOnly: false, transcription: null)!;

        article.Definite.Should().Be(definite);
        article.Indefinite.Should().Be(indefinite);
        article.IsElided.Should().BeFalse();
        article.Gender.Should().Be(gender);
    }

    [TestCase("arbre", GrammaticalGender.Masculine, "un")]
    [TestCase("école", GrammaticalGender.Feminine, "une")]
    [TestCase("élève", GrammaticalGender.Common, "un/une")]
    [TestCase("œuvre", GrammaticalGender.Feminine, "une")]
    public void ShouldElideBeforeAVowel(string headword, GrammaticalGender gender, string indefinite)
    {
        var article = FrenchArticles.For(headword, gender, isPluralOnly: false, transcription: null)!;

        article.Definite.Should().Be("l'");
        article.IsElided.Should().BeTrue();
        // The elided form hides the gender, so the indefinite article still carries it
        article.Indefinite.Should().Be(indefinite);
        article.WithDefinite(headword).Should().Be("l'" + headword);
    }

    [Test]
    public void ShouldElideBeforeASilentH()
    {
        var article = FrenchArticles.For("homme", GrammaticalGender.Masculine, false, "ɔm")!;

        article.WithDefinite("homme").Should().Be("l'homme");
    }

    [Test]
    public void ShouldNotElideBeforeAnHMarkedAsAspiratedInTheTranscription()
    {
        // WordReference renders hache as [ˈaʃ]
        var article = FrenchArticles.For("hachoir", GrammaticalGender.Masculine, false, "[ˈaʃwaʀ]")!;

        article.WithDefinite("hachoir").Should().Be("le hachoir");
    }

    [TestCase("hache", GrammaticalGender.Feminine, "la hache")]
    [TestCase("héros", GrammaticalGender.Masculine, "le héros")]
    [TestCase("haricot", GrammaticalGender.Masculine, "le haricot")]
    public void ShouldKnowCommonAspiratedHWordsWithoutATranscription(
        string headword, GrammaticalGender gender, string expected)
    {
        FrenchArticles.For(headword, gender, false, transcription: null)!
            .WithDefinite(headword).Should().Be(expected);
    }

    [Test]
    public void ShouldTrustTheAspiratedHListOverATranscriptionThatDoesNotMarkIt()
    {
        // A model-generated transcription may simply omit the mark
        FrenchArticles.For("hache", GrammaticalGender.Feminine, false, "aʃ")!
            .WithDefinite("hache").Should().Be("la hache");
    }

    [Test]
    public void ShouldIgnoreAStrayStressMarkOnAVowelInitialWord()
    {
        FrenchArticles.For("arbre", GrammaticalGender.Masculine, false, "ˈaʁbʁ")!
            .WithDefinite("arbre").Should().Be("l'arbre");
    }

    [TestCase("onze")]
    [TestCase("oui")]
    public void ShouldNotElideBeforeTheVowelInitialExceptions(string headword)
    {
        FrenchArticles.For(headword, GrammaticalGender.Masculine, false, null)!
            .Definite.Should().Be("le");
    }

    [TestCase("yaourt", "le")]
    [TestCase("yoga", "le")]
    [TestCase("ypérite", "l'")]
    public void ShouldTreatYBeforeAVowelAsAConsonant(string headword, string definite)
    {
        FrenchArticles.For(headword, GrammaticalGender.Masculine, false, null)!
            .Definite.Should().Be(definite);
    }

    [TestCase("gens", GrammaticalGender.Masculine)]
    [TestCase("vacances", GrammaticalGender.Feminine)]
    public void ShouldUseThePluralArticleForPluralOnlyNouns(string headword, GrammaticalGender gender)
    {
        var article = FrenchArticles.For(headword, gender, isPluralOnly: true, transcription: null)!;

        article.Definite.Should().Be("les");
        article.Indefinite.Should().Be("des");
        article.IsPlural.Should().BeTrue();
        // Gender is kept even though "les" does not show it, so it can still be coloured
        article.Gender.Should().Be(gender);
    }

    [Test]
    public void ShouldNotElideAPluralArticle()
    {
        FrenchArticles.For("affaires", GrammaticalGender.Feminine, isPluralOnly: true, null)!
            .WithDefinite("affaires").Should().Be("les affaires");
    }

    [Test]
    public void ShouldReturnNoArticleWhenTheGenderIsUnknown()
    {
        FrenchArticles.For("maison", gender: null, isPluralOnly: false, transcription: null)
            .Should().BeNull();
    }
}
