using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VocabularyBuilder.Application.FunctionalTests.Authentication;

using static Testing;

/// <summary>
/// Signing up and in over HTTP, and what is closed to someone who has not.
/// </summary>
/// <remarks>
/// These go through the whole pipeline, because that is where the rules live: the fallback
/// policy that closes every endpoint, the convention that opens Identity's public ones again,
/// and the filter in front of registration. A handler-level test would pass with all three
/// missing.
/// </remarks>
public class AuthenticationTests : BaseTestFixture
{
    private const string Password = "Testing1234!";

    private static async Task<HttpClient> SignedInClient(string email, bool useCookies = true)
    {
        await RunAsUserAsync(email, Password, Array.Empty<string>());

        var client = CreateClient();

        var login = await client.PostAsJsonAsync($"/api/Users/login?useCookies={useCookies}", new { email, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        if (!useCookies)
        {
            var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Test]
    public async Task ShouldRefuseTheApiToSomeoneNotSignedIn()
    {
        var response = await CreateClient().GetAsync("/api/fr/words");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The React app is what shows the login form, so the page it is served from cannot be
    /// behind the login too. Whether the built bundle is present depends on the build, so
    /// this asserts only that sign-in is not what stands in the way.
    /// </summary>
    [Test]
    public async Task ShouldServeTheAppItselfToSomeoneNotSignedIn()
    {
        var response = await CreateClient().GetAsync("/study");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ShouldRefuseRegistrationToAnEmailNotOnTheList()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/Users/register",
            new { email = "stranger@local", password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not allowed to register");
    }

    [Test]
    public async Task ShouldRegisterAnEmailOnTheListAndLetItSignIn()
    {
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/api/Users/register",
            new { email = CustomWebApplicationFactory.InvitedEmail, password = Password });

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/api/Users/login?useCookies=true",
            new { email = CustomWebApplicationFactory.InvitedEmail, password = Password });

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/Users/me");
        me.GetProperty("email").GetString().Should().Be(CustomWebApplicationFactory.InvitedEmail);
        me.GetProperty("isAdministrator").GetBoolean().Should().BeFalse();

        (await client.GetAsync("/api/fr/words")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ShouldRefuseAWrongPassword()
    {
        await RunAsUserAsync("member@local", Password, Array.Empty<string>());

        var login = await CreateClient().PostAsJsonAsync("/api/Users/login?useCookies=true",
            new { email = "member@local", password = "not-the-password" });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Opening Identity's public endpoints must not open the ones it protects along with them.
    /// </summary>
    [Test]
    public async Task ShouldKeepAccountManagementClosedToSomeoneNotSignedIn()
    {
        var response = await CreateClient().GetAsync("/api/Users/manage/info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// What curl and scripts use: no cookie, a token in the header instead.
    /// </summary>
    [Test]
    public async Task ShouldAcceptABearerTokenInPlaceOfTheCookie()
    {
        var client = await SignedInClient("member@local", useCookies: false);

        (await client.GetAsync("/api/fr/words")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ShouldEndTheSessionOnLogout()
    {
        var client = await SignedInClient("member@local");

        (await client.PostAsync("/api/Users/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/Users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ShouldSayWhenTheUserIsAnAdministrator()
    {
        await RunAsAdministratorAsync();

        var client = CreateClient();
        await client.PostAsJsonAsync("/api/Users/login?useCookies=true",
            new { email = "administrator@local", password = "Administrator1234!" });

        var me = await client.GetFromJsonAsync<JsonElement>("/api/Users/me");

        me.GetProperty("isAdministrator").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Frequency data is shared by everyone and read from a path on the server, so an
    /// ordinary account must not be able to load it.
    /// </summary>
    [Test]
    public async Task ShouldKeepTheFrequencyImportToAdministrators()
    {
        var client = await SignedInClient("member@local");

        var response = await client.PostAsync("/api/NewWords/import-frequency?lang=fr&filePath=/etc/passwd", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
