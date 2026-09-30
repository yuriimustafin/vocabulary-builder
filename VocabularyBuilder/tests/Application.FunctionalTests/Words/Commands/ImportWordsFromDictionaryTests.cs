using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.Words.Commands;

using static Testing;

/// <summary>
/// The dictionary import records the form each word was listed in, so study can ask about
/// it. What was listed is not always a form: the page accepts dictionary URLs as well.
/// </summary>
/// <remarks>
/// Answered by the recorded Oxford pages, which hand back what was searched exactly as the
/// real parser does - the URL included.
/// </remarks>
public class ImportWordsFromDictionaryTests : BaseTestFixture
{
    private static ImportWordsFromDictionaryCommand Command(string entry) =>
        new()
        {
            Words = new List<string> { entry },
            Language = Language.English,
            SourceType = DictionarySourceType.Oxford,
            ParseImmediately = true
        };

    [Test]
    public async Task ShouldNotRecordAPastedDictionaryUrlAsTheFormTheWordWasMetIn()
    {
        await SendAsync(Command("https://www.oxfordlearnersdictionaries.com/definition/english/test_1"));

        var encounter = (await ListAsync<WordEncounter>()).Should().ContainSingle().Subject;

        encounter.Form.Should().BeNull("a URL is not a form of the word, and no sentence could ever use it");
    }

    /// <remarks>
    /// Listed by its recording's name: only the full recorded pages (test_1, example_1)
    /// parse, the plain-word stubs beside them do not. What matters is that it is not a URL.
    /// </remarks>
    [Test]
    public async Task ShouldRecordAListedTermAsTheFormItWasMetIn()
    {
        await SendAsync(Command("test_1"));

        var encounter = (await ListAsync<WordEncounter>()).Should().ContainSingle().Subject;

        encounter.Form.Should().Be("test_1");
    }
}
