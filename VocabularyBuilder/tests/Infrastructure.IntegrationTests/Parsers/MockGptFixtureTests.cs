using System.Text.Json;
using FluentAssertions;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// Holds the recorded GPT responses to the schema <see cref="Infrastructure.Parsers.GptFrenchParser"/>
/// actually reads.
/// </summary>
/// <remarks>
/// Every one of these files had drifted to an older, flatter shape - <c>word</c>, <c>translation</c>,
/// a flat <c>examples</c> list - with no <c>senses</c>, <c>gender</c> or <c>ipa</c> anywhere. The
/// parser read them as words with no gender and no senses at all, and nothing failed, because the
/// GPT parser was only French's fallback and the recorded WordReference pages answered first. It
/// would have become visible the moment GPT was made the default source, as every mocked French
/// word arriving empty - and it would have looked like a bug in the new default rather than in
/// fixtures that had quietly stopped matching.
///
/// So this asserts the shape rather than any particular content. A schema change to the parser is
/// meant to fail here, which is the reminder to re-record.
/// </remarks>
public class MockGptFixtureTests
{
    private static IEnumerable<string> FixtureFiles() =>
        Directory.GetFiles(GptFrenchParserTests.FixturePath(), "*.json").OrderBy(path => path);

    [Test]
    public void ShouldHaveRecordedResponsesToReadAtAll()
    {
        FixtureFiles().Should().NotBeEmpty();
    }

    [TestCaseSource(nameof(FixtureFiles))]
    public void ShouldMatchTheSchemaTheParserReads(string path)
    {
        var recorded = JsonDocument.Parse(File.ReadAllText(path)).RootElement;

        recorded.TryGetProperty("Response", out var responseText)
            .Should().BeTrue("a recording is a prompt and the response to it");

        var response = JsonDocument.Parse(responseText.GetString()!).RootElement;
        var name = Path.GetFileNameWithoutExtension(path);

        foreach (var required in new[] { "lemma", "partOfSpeech", "senses" })
        {
            response.TryGetProperty(required, out _)
                .Should().BeTrue($"{name} has to carry '{required}', which the parser reads");
        }

        // The older shape, kept out by name so the failure says what is wrong
        foreach (var stale in new[] { "word", "translation" })
        {
            response.TryGetProperty(stale, out _)
                .Should().BeFalse($"'{stale}' is from the schema the parser stopped reading");
        }

        var senses = response.GetProperty("senses");
        senses.ValueKind.Should().Be(JsonValueKind.Array);
        senses.GetArrayLength().Should().BeGreaterThan(0, $"{name} needs at least one sense");

        foreach (var sense in senses.EnumerateArray())
        {
            sense.TryGetProperty("definition", out var definition).Should().BeTrue();
            definition.GetString().Should().NotBeNullOrWhiteSpace();

            sense.TryGetProperty("gloss", out _)
                .Should().BeTrue($"{name} must say either a gloss or null, so the field is exercised");

            foreach (var example in sense.GetProperty("examples").EnumerateArray())
            {
                example.GetProperty("french").GetString().Should().NotBeNullOrWhiteSpace();
                example.GetProperty("english").GetString().Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    /// <summary>
    /// A recording is found by its prompt, so one whose prompt does not name its own word can
    /// never be served - the client falls through to its generic stand-in answer instead, and
    /// the test that wanted the recording quietly gets a masculine noun with no IPA. Two
    /// recordings written without that line also collide, because the system prompt alone is
    /// identical for every word.
    /// </summary>
    [TestCaseSource(nameof(FixtureFiles))]
    public void ShouldBeFindableByAPromptThatNamesItsOwnWord(string path)
    {
        var recorded = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var word = Path.GetFileNameWithoutExtension(path);

        recorded.GetProperty("Prompt").GetString()
            .Should().Contain($"\"{word}\"",
                "MockGptClient looks a recording up by its prompt, and falls back to finding "
                + "the word quoted inside one");
    }

    /// <summary>
    /// A French noun needs a gender for <c>FrenchArticles</c> to derive its article, so a noun
    /// recorded without one would quietly reproduce the bare-headword bug the fill was written
    /// to fix.
    /// </summary>
    [TestCaseSource(nameof(FixtureFiles))]
    public void ShouldGiveEveryRecordedNounAGender(string path)
    {
        var recorded = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var response = JsonDocument.Parse(recorded.GetProperty("Response").GetString()!).RootElement;

        if (response.GetProperty("partOfSpeech").GetString() != "noun")
        {
            Assert.Pass("not a noun");
        }

        response.GetProperty("gender").GetString()
            .Should().BeOneOf("masculine", "feminine", "both");
    }

    /// <summary>
    /// The files are read as UTF-8 by the app and are full of accents; a fixture that lost its
    /// encoding would feed mojibake into every assertion downstream of it.
    /// </summary>
    [TestCaseSource(nameof(FixtureFiles))]
    public void ShouldStillBeReadableUtf8(string path)
    {
        File.ReadAllText(path).Should().NotContain("�", "the replacement character means the accents were lost");
    }
}
