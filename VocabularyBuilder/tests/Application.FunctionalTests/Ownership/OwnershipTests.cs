using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Application.Lists.Commands;
using VocabularyBuilder.Application.Lists.Queries;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Identity;

namespace VocabularyBuilder.Application.FunctionalTests.Ownership;

using static Testing;

/// <summary>
/// Each user sees and changes only their own vocabulary.
/// </summary>
/// <remarks>
/// None of the handlers here know about users. Everything asserted comes from the database
/// context, which scopes every query to the signed-in user and gives each new word to them -
/// so these go through the real handlers on purpose, to show that nothing on the way leaks.
/// </remarks>
public class OwnershipTests : BaseTestFixture
{
    private static Task<string> RunAsSomeoneElseAsync() =>
        RunAsUserAsync("someone.else@local", "Testing1234!", Array.Empty<string>());

    private static Task<int> CreateWord(string headword) =>
        SendAsync(new CreateWordCommand { Headword = headword, Language = Language.French, Frequency = 1 });

    [Test]
    public async Task ShouldGiveANewWordToWhoeverCreatedIt()
    {
        var userId = GetUserId();

        var id = await CreateWord("maison");

        var word = (await ListAsync<Word>()).Single(w => w.Id == id);
        word.OwnerId.Should().Be(userId);
    }

    [Test]
    public async Task ShouldNotListAnotherUsersWords()
    {
        await CreateWord("maison");

        await RunAsSomeoneElseAsync();

        var theirs = await SendAsync(new GetWordsQuery(Language.French));
        theirs.Items.Should().BeEmpty();

        await RunAsDefaultUserAsync();

        var mine = await SendAsync(new GetWordsQuery(Language.French));
        mine.Items.Should().ContainSingle(w => w.Headword == "maison");
    }

    /// <summary>
    /// An id is not a secret - they are sequential - so knowing one must not be enough.
    /// </summary>
    [Test]
    public async Task ShouldNotFindAnotherUsersWordById()
    {
        var id = await CreateWord("maison");

        await RunAsSomeoneElseAsync();

        (await SendAsync(new GetWordQuery(id))).Should().BeNull();
    }

    [Test]
    public async Task ShouldNotLetAnotherUserDeleteAWord()
    {
        var id = await CreateWord("maison");

        await RunAsSomeoneElseAsync();

        await FluentActions.Invoking(() => SendAsync(new DeleteWordCommand(id)))
            .Should().ThrowAsync<NotFoundException>();

        await RunAsDefaultUserAsync();

        (await SendAsync(new GetWordQuery(id))).Should().NotBeNull();
    }

    /// <summary>
    /// Headwords are unique per user rather than across the database, so one learner having a
    /// word does not stop another from adding it.
    /// </summary>
    [Test]
    public async Task ShouldLetTwoUsersEachHaveTheSameWord()
    {
        var mine = await CreateWord("maison");

        await RunAsSomeoneElseAsync();

        var theirs = await CreateWord("maison");

        theirs.Should().NotBe(mine);
    }

    /// <summary>
    /// Rows under a word are filtered through it, since several of them are queried directly
    /// rather than reached from the word.
    /// </summary>
    [Test]
    public async Task ShouldHideWhatHangsOffAnotherUsersWord()
    {
        // Creating a word records the encounter that brought it in
        await CreateWord("maison");

        (await CountAsync<WordEncounter>()).Should().Be(1);

        await RunAsSomeoneElseAsync();

        (await CountAsync<WordEncounter>()).Should().Be(0);
    }

    [Test]
    public async Task ShouldNotShowOrLetAnotherUserAddToAList()
    {
        var listId = await SendAsync(new CreateListCommand { Title = "Kitchen", Language = Language.French });

        await RunAsSomeoneElseAsync();

        (await SendAsync(new GetListsQuery(Language.French))).Should().BeEmpty();

        await FluentActions.Invoking(() => SendAsync(new CreateListItemCommand { ListId = listId, Text = "la poêle" }))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldSeeNothingWithNobodySignedIn()
    {
        await CreateWord("maison");

        RunAsAnonymous();

        (await ListAsync<Word>()).Should().BeEmpty();
    }

    /// <summary>
    /// With nobody to own it, a word must not be written with an empty owner and picked up by
    /// whoever the filter happens to match.
    /// </summary>
    [Test]
    public async Task ShouldRefuseToSaveAWordWithNobodySignedIn()
    {
        RunAsAnonymous();

        await FluentActions.Invoking(() => AddAsync(new Word { Headword = "maison", Language = Language.French }))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Counted across every owner, because a filtered count would come out empty once the
    /// user was gone whether the words had gone with them or not.
    /// </summary>
    [Test]
    public async Task ShouldRemoveAUsersWordsWithTheUser()
    {
        await RunAsSomeoneElseAsync();
        await CreateWord("maison");

        (await CountEveryonesWords()).Should().Be(1);

        var deleted = await WithServiceAsync<UserManager<ApplicationUser>, IdentityResult>(async users =>
            await users.DeleteAsync((await users.FindByNameAsync("someone.else@local"))!));

        deleted.Succeeded.Should().BeTrue();
        (await CountEveryonesWords()).Should().Be(0);
    }

    private static Task<int> CountEveryonesWords() =>
        WithServiceAsync<ApplicationDbContext, int>(context => context.Words.IgnoreQueryFilters().CountAsync());
}
