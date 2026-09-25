using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Identity;

namespace VocabularyBuilder.Application.FunctionalTests.Authentication;

using static Testing;

/// <summary>
/// Creating the administrator on startup, and handing it the data from before users existed.
/// </summary>
/// <remarks>
/// The placeholder is made here the way the AddUserOwnership migration makes it - a bare row,
/// no email, no password - rather than through UserManager, which would refuse to create it.
/// A placeholder built any other way would not be the one real databases contain, and the
/// takeover once passed every test while failing on the first real database it met.
/// </remarks>
public class AdministratorBootstrapTests : BaseTestFixture
{
    private const string Email = "owner@local";
    private const string Password = "Owner-Passw0rd!";

    private static Task SeedAdministrator(string? email = Email, string? password = Password) =>
        WithServiceAsync<IServiceProvider, bool>(async services =>
        {
            var initialiser = ActivatorUtilities.CreateInstance<ApplicationDbContextInitialiser>(
                services, Options.Create(new AdministratorOptions { Email = email, Password = password }));

            await initialiser.SeedAdministratorAsync();

            return true;
        });

    private static Task<int> GivenDataFromBeforeUsers() =>
        WithServiceAsync<ApplicationDbContext, int>(async context =>
        {
            context.Users.Add(new ApplicationUser
            {
                Id = LegacyOwner.Id,
                UserName = LegacyOwner.UserName,
                NormalizedUserName = LegacyOwner.UserName.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString("N")
            });

            var word = new Word { Headword = "maison", Language = Language.French, OwnerId = LegacyOwner.Id };
            context.Words.Add(word);

            await context.SaveChangesAsync();

            return word.Id;
        });

    private static Task<ApplicationUser?> FindUser(string id) =>
        WithServiceAsync<UserManager<ApplicationUser>, ApplicationUser?>(users => users.FindByIdAsync(id));

    private static Task<bool> CanSignIn(string email, string password) =>
        WithServiceAsync<UserManager<ApplicationUser>, bool>(async users =>
        {
            var user = await users.FindByEmailAsync(email);

            return user is not null && await users.CheckPasswordAsync(user, password);
        });

    private static Task<bool> IsAdministrator(string email) =>
        WithServiceAsync<UserManager<ApplicationUser>, bool>(async users =>
            await users.IsInRoleAsync((await users.FindByEmailAsync(email))!, Roles.Administrator));

    [Test]
    public async Task ShouldCreateTheAdministratorWhenThereIsNothingToTakeOver()
    {
        await SeedAdministrator();

        (await CanSignIn(Email, Password)).Should().BeTrue();
        (await IsAdministrator(Email)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldHandTheDataFromBeforeUsersToTheAdministrator()
    {
        var wordId = await GivenDataFromBeforeUsers();

        await SeedAdministrator();

        var administrator = await FindUser(LegacyOwner.Id);
        administrator!.Email.Should().Be(Email);
        (await CanSignIn(Email, Password)).Should().BeTrue();
        (await IsAdministrator(Email)).Should().BeTrue();

        // Signed in as the administrator, the old word is simply there
        await RunAsUserAsync(Email, Password, Array.Empty<string>());
        (await ListAsync<Word>()).Should().ContainSingle(w => w.Id == wordId);
    }

    [Test]
    public async Task ShouldLeaveThePlaceholderAloneWhenThePasswordIsRefused()
    {
        await GivenDataFromBeforeUsers();

        await FluentActions.Invoking(() => SeedAdministrator(password: "weak"))
            .Should().ThrowAsync<InvalidOperationException>();

        var placeholder = await FindUser(LegacyOwner.Id);
        placeholder!.UserName.Should().Be(LegacyOwner.UserName);
        placeholder.Email.Should().BeNull();
    }

    /// <summary>
    /// The configured password creates the account and nothing more, so one changed since is
    /// not put back on the next start.
    /// </summary>
    [Test]
    public async Task ShouldNotResetAnExistingAdministratorsPassword()
    {
        await SeedAdministrator();

        await SeedAdministrator(password: "Another-Passw0rd!");

        (await CanSignIn(Email, Password)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldDoNothingWhenNoAdministratorIsConfigured()
    {
        await GivenDataFromBeforeUsers();

        await SeedAdministrator(email: null, password: null);

        (await FindUser(LegacyOwner.Id))!.UserName.Should().Be(LegacyOwner.UserName);
        (await WithServiceAsync<ApplicationDbContext, int>(context => context.Users.CountAsync()))
            .Should().Be(2, "the placeholder and the signed-in test user, and nobody new");
    }
}
