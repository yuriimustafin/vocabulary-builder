using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Domain.Entities;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Domain.UnitTests.Entities;

/// <summary>
/// What counts as a word still waiting on a dictionary.
/// </summary>
public class WordDictionaryDataTests
{
    private static Word FrenchNoun(
        GrammaticalGender? gender = GrammaticalGender.Feminine,
        string? partOfSpeech = "nf",
        bool withSense = true)
    {
        return new Word
        {
            Headword = "randonnée",
            Language = Language.French,
            PartOfSpeech = partOfSpeech,
            Gender = gender,
            Senses = withSense
                ? new List<Sense> { new() { Definition = "a long walk", Examples = new List<string>() } }
                : new List<Sense>()
        };
    }

    /// <summary>
    /// The case that started this: imported as a bare headword, so nothing but the word.
    /// </summary>
    [Test]
    public void ShouldWantFillingWhenNothingHasBeenLookedUp()
    {
        var word = new Word { Headword = "randonnée", Language = Language.French };

        word.IsMissingDictionaryData().Should().BeTrue();
    }

    [Test]
    public void ShouldWantFillingWithoutAPartOfSpeech()
    {
        FrenchNoun(partOfSpeech: null).IsMissingDictionaryData().Should().BeTrue();
    }

    [Test]
    public void ShouldWantFillingWithoutASense()
    {
        FrenchNoun(withSense: false).IsMissingDictionaryData().Should().BeTrue();
    }

    /// <summary>
    /// A definition can arrive from a model while the gender does not, and a noun without a
    /// gender is studied with no article - which looks like the article feature being broken.
    /// </summary>
    [Test]
    public void ShouldWantFillingWhenAFrenchNounHasEverythingButItsGender()
    {
        FrenchNoun(gender: null).IsMissingDictionaryData().Should().BeTrue();
    }

    [Test]
    public void ShouldBeSatisfiedWhenTheNounHasItsGender()
    {
        FrenchNoun().IsMissingDictionaryData().Should().BeFalse();
    }

    /// <summary>
    /// Only nouns have a gender, so a verb without one is finished, not waiting.
    /// </summary>
    [TestCase("v")]
    [TestCase("vtr")]
    [TestCase("adj")]
    [TestCase("adv")]
    public void ShouldNotWantFillingForAWordThatHasNoGenderToHave(string partOfSpeech)
    {
        FrenchNoun(gender: null, partOfSpeech: partOfSpeech)
            .IsMissingDictionaryData().Should().BeFalse();
    }

    /// <summary>
    /// English nouns have no gender either, so the gender rule is French only.
    /// </summary>
    [Test]
    public void ShouldNotWantFillingForAnEnglishNounWithoutGender()
    {
        var word = new Word
        {
            Headword = "hike",
            Language = Language.English,
            PartOfSpeech = "noun",
            Senses = new List<Sense> { new() { Definition = "a long walk", Examples = new List<string>() } }
        };

        word.IsMissingDictionaryData().Should().BeFalse();
    }

    /// <summary>
    /// A filled noun has an article to show; that is the whole point of filling it.
    /// </summary>
    [Test]
    public void ShouldHaveAnArticleOnceItsGenderIsKnown()
    {
        FrenchNoun(gender: null).GetArticle().Should().BeNull();

        var filled = FrenchNoun();

        filled.GetArticle().Should().NotBeNull();
        filled.GetArticle()!.Indefinite.Should().Be("une");
    }
}
