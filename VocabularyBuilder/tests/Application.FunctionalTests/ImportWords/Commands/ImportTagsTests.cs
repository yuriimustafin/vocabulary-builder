using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.ImportWords.Commands;

using static Testing;

/// <summary>
/// Tagging an import, against a real database, because what matters is what the word ends up
/// carrying after it has been imported more than once.
/// </summary>
public class ImportTagsTests : BaseTestFixture
{
    private const string Header =
        "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2";

    private static ImportLingQWordsCommand Import(string rows, string? tag, string? listName = null) =>
        new()
        {
            FileContent = Header + "\n" + rows,
            Language = Language.French,
            ListName = listName,
            Tag = tag
        };

    private static async Task<List<string>> TagsOf(string headword)
    {
        var words = await ListAsync<Word>();
        return words.Single(w => w.Headword == headword).Tags?.ToList() ?? new List<string>();
    }

    [Test]
    public async Task ShouldTagEveryWordTheImportBringsIn()
    {
        await SendAsync(Import(
            "une main,,,,,,,,,\nun pied,,,,,,,,,\nune conférence,,,,,,,,,\n",
            tag: "preply"));

        var words = await ListAsync<Word>();

        words.Should().HaveCount(3);
        words.Should().OnlyContain(w => w.Tags!.Contains("preply"));
    }

    [Test]
    public async Task ShouldApplySeveralTagsGivenInOneField()
    {
        await SendAsync(Import("une main,,,,,,,,,\n", tag: "preply, travel"));

        (await TagsOf("main")).Should().Equal("preply", "travel");
    }

    /// <summary>
    /// The point of tags: a word met again under a new label is still the word it was.
    /// </summary>
    [Test]
    public async Task ShouldAddToTheTagsAWordAlreadyHas()
    {
        await SendAsync(Import("une main,,,,,,,,,\n", tag: "preply", listName: "first"));
        await SendAsync(Import("une main,,,,,,,,,\n", tag: "travel", listName: "second"));

        (await TagsOf("main")).Should().Equal("preply", "travel");
        (await ListAsync<Word>()).Should().ContainSingle();
    }

    [Test]
    public async Task ShouldNotRepeatATagTheWordAlreadyHas()
    {
        await SendAsync(Import("une main,,,,,,,,,\n", tag: "preply", listName: "first"));
        await SendAsync(Import("une main,,,,,,,,,\n", tag: "Preply", listName: "second"));

        (await TagsOf("main")).Should().Equal("preply");
    }

    /// <summary>
    /// Two forms of one word arrive as two rows but one word, and the tag lands once.
    /// </summary>
    [Test]
    public async Task ShouldTagAWordOnceHoweverManyRowsReachIt()
    {
        await SendAsync(Import(
            "une randonnée,,,,,,,,,\nla randonnée,,,,,,,,,\n",
            tag: "preply"));

        (await TagsOf("randonnée")).Should().Equal("preply");
    }

    /// <summary>
    /// A word already in the vocabulary keeps everything it had; the tag is the only addition.
    /// </summary>
    [Test]
    public async Task ShouldTagAWordThatWasAlreadyThere()
    {
        await AddAsync(new Word
        {
            Headword = "main",
            Language = Language.French,
            PartOfSpeech = "nf",
            Tags = new List<string> { "kindle" }
        });

        await SendAsync(Import("une main,,,,,,,,,\n", tag: "preply"));

        var word = (await ListAsync<Word>()).Single();

        word.Tags.Should().Equal("kindle", "preply");
        word.PartOfSpeech.Should().Be("nf", "tagging must not disturb what the word already knows");
    }

    [Test]
    public async Task ShouldLeaveAWordUntaggedWhenNoTagIsGiven()
    {
        await SendAsync(Import("une main,,,,,,,,,\n", tag: null));

        (await TagsOf("main")).Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task ShouldIgnoreAnEmptyTagField(string tag)
    {
        await SendAsync(Import("une main,,,,,,,,,\n", tag));

        (await TagsOf("main")).Should().BeEmpty();
    }

    /// <summary>
    /// Re-importing the same export adds no encounter, and must not add a tag twice either.
    /// </summary>
    [Test]
    public async Task ShouldNotDuplicateTagsWhenTheSameExportIsImportedAgain()
    {
        var command = Import("une main,,,,,,,,,\n", tag: "preply", listName: "Preply");

        await SendAsync(command);
        await SendAsync(command);

        (await TagsOf("main")).Should().Equal("preply");
    }

    [Test]
    public async Task ShouldTagWordsImportedFromLessonNotes()
    {
        await SendAsync(new ImportLessonNotesCommand
        {
            Notes = "une main=hand",
            Language = Language.French,
            Tag = "lesson 12"
        });

        var words = await ListAsync<Word>();

        words.Should().NotBeEmpty();
        words.Should().OnlyContain(w => w.Tags!.Contains("lesson 12"));
    }
}
