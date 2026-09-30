using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class TypedAnswerTests
{
    private static StudyMaterial French(string headword, string? gender = null) => new()
    {
        WordId = 1,
        Headword = headword,
        Language = Language.French,
        Meaning = "a meaning",
        Article = gender is null ? null : new NounArticleDto { Gender = gender, Definite = gender == "feminine" ? "la" : "le" }
    };

    private static StudyMaterial English(string headword) => new()
    {
        WordId = 1,
        Headword = headword,
        Language = Language.English,
        Meaning = "a meaning"
    };

    private static TypedMatchKind Kind(string typed, StudyMaterial material) => TypedAnswer.Match(typed, material).Kind;

    [TestCase("chaise")]
    [TestCase("  Chaise ")]
    [TestCase("CHAISE")]
    public void CaseAndSurroundingSpaceDoNotMatter(string typed)
    {
        Kind(typed, French("chaise")).Should().Be(TypedMatchKind.Exact);
    }

    [Test]
    public void AHyphenAndASpaceAreTheSame()
    {
        Kind("au dessus", French("au-dessus")).Should().Be(TypedMatchKind.Exact);
        Kind("montagne-russe", French("montagne russe")).Should().Be(TypedMatchKind.Exact);
    }

    [Test]
    public void CurlyAndStraightApostrophesAreTheSame()
    {
        Kind("aujourd’hui", French("aujourd'hui")).Should().Be(TypedMatchKind.Exact);
    }

    [Test]
    public void TheLigatureMayBeSpelledOut()
    {
        Kind("oeuvre", French("œuvre")).Should().Be(TypedMatchKind.Exact);
    }

    [TestCase("la chaise")]
    [TestCase("une chaise")]
    public void ALeadingArticleOfTheRightGenderIsAccepted(string typed)
    {
        Kind(typed, French("chaise", "feminine")).Should().Be(TypedMatchKind.Exact);
    }

    [TestCase("l'arbre")]
    [TestCase("l' arbre")]
    [TestCase("les arbres")]
    public void AnArticleThatDoesNotShowGenderIsJustIgnored(string typed)
    {
        // "les arbres" is not "arbre", but the article itself cannot be the problem.
        var expected = typed.Contains("arbres") ? TypedMatchKind.Typo : TypedMatchKind.Exact;
        Kind(typed, French("arbre", "masculine")).Should().Be(expected);
    }

    [TestCase("le chaise")]
    [TestCase("un chaise")]
    public void TheWrongGendersArticleIsCaughtThoughTheWordIsRight(string typed)
    {
        Kind(typed, French("chaise", "feminine")).Should().Be(TypedMatchKind.WrongArticle);
    }

    [Test]
    public void ANounThatTakesEitherGenderTakesEitherArticle()
    {
        Kind("le élève", French("élève", "common")).Should().Be(TypedMatchKind.Exact);
        Kind("la élève", French("élève", "common")).Should().Be(TypedMatchKind.Exact);
    }

    [Test]
    public void AHeadwordThatStartsWithAnArticleKeepsIt()
    {
        Kind("la plupart", French("la plupart")).Should().Be(TypedMatchKind.Exact);
    }

    [Test]
    public void EnglishArticlesAndTheInfinitiveToAreOptional()
    {
        Kind("to remember", English("remember")).Should().Be(TypedMatchKind.Exact);
        Kind("the window", English("window")).Should().Be(TypedMatchKind.Exact);
    }

    [TestCase("eleve")]
    [TestCase("élêve")]
    public void AMissingOrWrongAccentIsOnlyAlmost(string typed)
    {
        Kind(typed, French("élève")).Should().Be(TypedMatchKind.AccentsOnly);
    }

    [TestCase("remeber")]    // missing letter
    [TestCase("remembber")]  // extra letter
    [TestCase("remamber")]   // wrong letter
    [TestCase("rememebr")]   // two letters swapped
    public void OneSlipInALongEnoughWordIsATypo(string typed)
    {
        Kind(typed, English("remember")).Should().Be(TypedMatchKind.Typo);
    }

    [Test]
    public void AccentsAndASlipTogetherAreStillATypo()
    {
        Kind("extraterestre", French("extraterrestre")).Should().Be(TypedMatchKind.Typo);
        Kind("eleves", French("élève")).Should().Be(TypedMatchKind.Typo);
    }

    [Test]
    public void OneSlipInAShortWordIsWrong()
    {
        // In a short word a single letter is usually a different word: chat, chas, chaton.
        Kind("chas", French("chat")).Should().Be(TypedMatchKind.Wrong);
    }

    [Test]
    public void TwoSlipsAreWrong()
    {
        Kind("rememver", English("remember")).Should().Be(TypedMatchKind.Typo);
        Kind("rmemver", English("remember")).Should().Be(TypedMatchKind.Wrong);
    }

    [Test]
    public void NothingTypedIsWrong()
    {
        Kind("", English("remember")).Should().Be(TypedMatchKind.Wrong);
        Kind("the ", English("remember")).Should().Be(TypedMatchKind.Wrong);
    }

    [Test]
    public void TheMatchCarriesTheWordAsTypedWithoutItsArticle()
    {
        TypedAnswer.Match("La Chaisse", French("chaise", "feminine")).Word.Should().Be("chaisse");
    }

    [Test]
    public void TheDistanceCountsASwapAsOneSlip()
    {
        TypedAnswer.Distance("abcd", "abdc").Should().Be(1);
        TypedAnswer.Distance("abcd", "abcd").Should().Be(0);
        TypedAnswer.Distance("abcd", "xbcy").Should().Be(2);
    }
}
