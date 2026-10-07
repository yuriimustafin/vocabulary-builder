using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class SyllabifierTests
{
    private static string French(string word) => string.Join("-", Syllabifier.Split(word, Language.French));

    private static string English(string word) => string.Join("-", Syllabifier.Split(word, Language.English));

    [TestCase("chocolat", "cho-co-lat")]      // one consonant starts the next syllable
    [TestCase("abricot", "a-bri-cot")]        // obstruent + r stays together
    [TestCase("table", "ta-ble")]             // ...and keeps its mute e as a tile
    [TestCase("fenêtre", "fe-nê-tre")]
    [TestCase("arbre", "ar-bre")]             // three consonants: before the br
    [TestCase("chanteur", "chan-teur")]       // two others split between them
    [TestCase("attention", "at-ten-tion")]    // double consonants split
    [TestCase("oiseau", "oi-seau")]           // vowel groups stay whole
    [TestCase("magnifique", "ma-gni-fique")]  // gn and qu are one consonant; mute e joins back
    [TestCase("guitare", "gui-tare")]         // gu before a vowel is one consonant
    [TestCase("création", "cré-a-tion")]      // é ends its syllable
    [TestCase("naïf", "na-ïf")]               // a tréma splits the vowels
    [TestCase("exercice", "ex-er-cice")]      // x stays behind
    public void FrenchWordsSplitIntoTheirWrittenSyllables(string word, string expected)
    {
        French(word).Should().Be(expected);
    }

    [TestCase("window", "win-dow")]
    [TestCase("remember", "re-mem-ber")]
    [TestCase("table", "ta-ble")]             // consonant + le is a syllable
    [TestCase("little", "lit-tle")]
    [TestCase("make", "make")]                // a silent final e is not
    [TestCase("wanted", "wan-ted")]           // -ed after t is
    [TestCase("jumped", "jumped")]            // ...and otherwise is not
    [TestCase("pocket", "pock-et")]           // ck stays behind
    public void EnglishWordsSplitIntoTheirWrittenSyllables(string word, string expected)
    {
        English(word).Should().Be(expected);
    }

    [Test]
    public void ThePiecesAlwaysJoinBackIntoTheWord()
    {
        foreach (var word in new[] { "extraterrestre", "représentant", "aujourd'hui", "Écoute", "œuvre" })
        {
            string.Concat(Syllabifier.Split(word, Language.French)).Should().Be(word);
        }
    }

    [Test]
    public void CaseAndAccentsAreKeptAsWritten()
    {
        French("Éléphant").Should().Be("É-lé-phant");
    }

    [Test]
    public void EachWordOfAPhraseIsSplitOnItsOwnWithoutTheSpaces()
    {
        French("montagne russe").Should().Be("mon-tagne-russe");
        French("au-dessus").Should().Be("au-des-sus");
    }

    [Test]
    public void AnElisionStaysWithTheWordItBelongsTo()
    {
        French("chef d'équipe").Should().Be("chef-d'é-quipe");
    }

    [Test]
    public void AOneSyllableWordIsOnePiece()
    {
        French("bruit").Should().Be("bruit");
        English("strength").Should().Be("strength");
    }

    // --- chunks ----------------------------------------------------------------

    private static string Chunks(string word, Language language = Language.French) =>
        string.Join("|", Syllabifier.Chunks(word, language));

    [TestCase("chocolat", "cho|co|lat")]
    [TestCase("fenêtre", "fe|nê|tre")]
    [TestCase("au-delà", "au|de|là")]
    public void SyllablesAreThePiecesWhenTheyMakeThreeOrFour(string word, string expected)
    {
        Chunks(word).Should().Be(expected);
    }

    [TestCase("fan", "f|a|n")]
    [TestCase("oui", "o|u|i")]
    public void AWordOfThreeLettersOrFewerIsItsLetters(string word, string expected)
    {
        Chunks(word).Should().Be(expected);
    }

    [TestCase("panneau")]
    [TestCase("quart")]
    [TestCase("exprès")]
    [TestCase("approfondissement")]
    [TestCase("supprimer")]
    [TestCase("élève")]
    public void EveryLongerWordComesInThreeOrFourPiecesThatJoinBackIntoIt(string word)
    {
        var pieces = Syllabifier.Chunks(word, Language.French);

        pieces.Count.Should().BeInRange(Syllabifier.FewestChunks, Syllabifier.MostChunks);
        string.Concat(pieces).Should().Be(word.Replace("-", string.Empty));
    }

    [Test]
    public void ASyllableIsSplitWithoutPartingADigraphWhereThereIsAnotherPlace()
    {
        // One syllable, so it has to be split - but never between q and u
        Syllabifier.Chunks("quart", Language.French).Should().StartWith("qu");
    }
}
