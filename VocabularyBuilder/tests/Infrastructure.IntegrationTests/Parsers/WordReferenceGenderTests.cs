using FluentAssertions;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// Gender, number and the resulting article, read from pages recorded from
/// wordreference.com. Each fixture covers one way a noun can be tagged.
/// </summary>
public class WordReferenceGenderTests
{
    private static async Task<Word> Parse(string word)
    {
        var html = await File.ReadAllTextAsync(WordReferenceFrenchParserTests.FixturePath($"{word}.html"));

        var parser = new WordReferenceFrenchParser(
            Options.Create(new WordReferenceOptions { MaxTranslations = 5, RequestDelayMilliseconds = 0 }),
            new MockWordReferencePageLoader(WordReferenceFrenchParserTests.FixturePath("")),
            new WordReferenceConjugationParser());

        return (await parser.GetWordFromCachedHtml(html))!;
    }

    [Test]
    public async Task ShouldReadAFeminineNoun()
    {
        // "maison" is tagged nf
        var word = await Parse("maison");

        word.Gender.Should().Be(GrammaticalGender.Feminine);
        word.IsPluralOnly.Should().BeFalse();
        word.GetArticle()!.WithDefinite(word.Headword).Should().Be("la maison");
    }

    [Test]
    public async Task ShouldReadAMasculineNounStartingWithAVowel()
    {
        // "arbre" is tagged nm; the elided article hides that, the indefinite keeps it
        var word = await Parse("arbre");

        word.Gender.Should().Be(GrammaticalGender.Masculine);
        var article = word.GetArticle()!;
        article.WithDefinite(word.Headword).Should().Be("l'arbre");
        article.Indefinite.Should().Be("un");
    }

    [Test]
    public async Task ShouldElideBeforeASilentH()
    {
        // WordReference transcribes "homme" as [ɔm], with no aspiration mark
        var word = await Parse("homme");

        word.GetArticle()!.WithDefinite(word.Headword).Should().Be("l'homme");
    }

    [Test]
    public async Task ShouldNotElideBeforeAnAspiratedH()
    {
        // ...and "hache" as [ˈaʃ], where the mark blocks elision
        var word = await Parse("hache");

        word.Transcription.Should().StartWith("ˈ");
        word.GetArticle()!.WithDefinite(word.Headword).Should().Be("la hache");
    }

    [Test]
    public async Task ShouldKeepEachSenseGenderWhenTheyDiffer()
    {
        // "livre" is a book (nm) and a pound (nf)
        var word = await Parse("livre");

        word.Gender.Should().Be(GrammaticalGender.Masculine);
        word.Senses!.Select(s => s.Gender).Should().Contain(GrammaticalGender.Masculine);
        word.Senses!.Select(s => s.Gender).Should().Contain(GrammaticalGender.Feminine);

        var pound = word.Senses!.First(s => s.Gender == GrammaticalGender.Feminine);
        word.GetArticle(pound)!.WithDefinite(word.Headword).Should().Be("la livre");
    }

    [Test]
    public async Task ShouldReadANounOfEitherSex()
    {
        // "élève" is tagged nmf
        var word = await Parse("élève");

        word.Gender.Should().Be(GrammaticalGender.Common);
        var article = word.GetArticle()!;
        article.WithDefinite(word.Headword).Should().Be("l'élève");
        article.Indefinite.Should().Be("un/une");
    }

    [Test]
    public async Task ShouldReadANounOfEitherGender()
    {
        // "après-midi" is tagged "nm ou nf inv" - the first tag alone would say masculine
        var word = await Parse("après-midi");

        word.Gender.Should().Be(GrammaticalGender.Common);
    }

    [TestCase("gens", GrammaticalGender.Masculine)]
    [TestCase("vacances", GrammaticalGender.Feminine)]
    public async Task ShouldReadAPluralOnlyNoun(string headword, GrammaticalGender gender)
    {
        // "gens" is nmpl, "vacances" nfpl
        var word = await Parse(headword);

        word.Gender.Should().Be(gender);
        word.IsPluralOnly.Should().BeTrue();
        word.GetArticle()!.WithDefinite(word.Headword).Should().Be($"les {headword}");
    }

    [Test]
    public async Task ShouldGiveNoArticleToAVerb()
    {
        var word = await Parse("prendre");

        word.Gender.Should().BeNull();
        word.GetArticle().Should().BeNull();
        word.Senses!.Should().OnlyContain(s => s.Gender == null);
    }
}
