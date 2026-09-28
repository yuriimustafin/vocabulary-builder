using System.Security.Claims;

using VocabularyBuilder.Application.Common.Interfaces;

namespace VocabularyBuilder.Web.Services;

/// <summary>
/// The signed-in user of the current request, or whoever background work was told to act as.
/// </summary>
public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _actingAs;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _actingAs ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Makes the rest of this scope act as the given user.
    /// </summary>
    /// <remarks>
    /// For work that happens away from any request - the enrichment worker filling in a word -
    /// and so has nobody signed in. Without it that work would see no data at all, because
    /// every owned query is scoped to the current user. It is set on the scope's own instance,
    /// so it ends with the scope and never leaks into a request.
    /// </remarks>
    public void ActAs(string userId)
    {
        _actingAs = userId;
    }
}
