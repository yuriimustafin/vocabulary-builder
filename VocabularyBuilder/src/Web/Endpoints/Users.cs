using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Infrastructure.Identity;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>
/// Registering, signing in and out, and finding out who is signed in.
/// </summary>
/// <remarks>
/// The accounts themselves are ASP.NET Core Identity's: <c>MapIdentityApi</c> supplies
/// /register, /login, /refresh and the /manage endpoints, and this class only adds what it
/// leaves out. Log in with <c>?useCookies=true</c> for a browser session, or without it for a
/// bearer token to send from curl.
/// </remarks>
public class Users : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this);

        var identity = group.MapIdentityApi<ApplicationUser>();
        identity.AddEndpointFilter(RefuseUnlistedRegistrations);
        identity.Finally(AllowAnonymousUnlessAuthorized);

        group.MapGet("/me", GetCurrentUser);
        group.MapPost("/logout", Logout);
    }

    public async Task<Results<Ok<CurrentUserDto>, UnauthorizedHttpResult>> GetCurrentUser(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);

        // A valid cookie for an account that has since been deleted
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        // Asked of the store rather than read from the cookie's claims, which are only as
        // fresh as the sign-in they were issued at
        var isAdministrator = await userManager.IsInRoleAsync(user, Roles.Administrator);

        return TypedResults.Ok(new CurrentUserDto(user.Email ?? user.UserName ?? string.Empty, isAdministrator));
    }

    public async Task<NoContent> Logout(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Turns a registration away before Identity sees it unless its email is on the allowlist.
    /// </summary>
    private static async ValueTask<object?> RefuseUnlistedRegistrations(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var registration = context.Arguments.OfType<RegisterRequest>().FirstOrDefault();

        if (registration is not null)
        {
            var options = context.HttpContext.RequestServices
                .GetRequiredService<IOptionsSnapshot<RegistrationOptions>>().Value;

            if (!options.IsAllowed(registration.Email))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Email"] = ["This email address is not allowed to register."]
                });
            }
        }

        return await next(context);
    }

    /// <summary>
    /// Opens the Identity endpoints that are meant to be public.
    /// </summary>
    /// <remarks>
    /// Register, login, refresh and password reset carry no authorization metadata of their
    /// own, so the fallback policy would otherwise demand a signed-in user before letting
    /// anyone sign in. The /manage endpoints do require authorization and are left as they
    /// are. It has to run as a final convention: the /manage group adds its requirement as an
    /// ordinary one, and only by now is it there to be seen.
    /// </remarks>
    private static void AllowAnonymousUnlessAuthorized(EndpointBuilder endpoint)
    {
        if (!endpoint.Metadata.OfType<IAuthorizeData>().Any())
        {
            endpoint.Metadata.Add(new AllowAnonymousAttribute());
        }
    }
}

public record CurrentUserDto(string Email, bool IsAdministrator);
