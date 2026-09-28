using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VocabularyBuilder.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseSchemaOnlyAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
    }

    /// <summary>
    /// Makes sure the configured administrator can sign in. Runs on every start, in every
    /// environment, and does nothing once the account exists.
    /// </summary>
    public static async Task InitialiseAdministratorAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.SeedAdministratorAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdministratorOptions _administrator;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<AdministratorOptions> administrator)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _administrator = administrator.Value;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (InMemoryDatabase.IsInMemory(_context.Database.GetConnectionString()))
            {
                // For in-memory database, use EnsureCreated instead of migrations
                await _context.Database.EnsureCreatedAsync();
            }
            else
            {
                // For file-based database, apply migrations
                await _context.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    /// <summary>
    /// Creates the administrator from configuration, or hands it the data that predates users.
    /// </summary>
    /// <remarks>
    /// A failure here stops the application. An administrator that was configured and could
    /// not be created - a password the validators reject, most often - leaves a deployment
    /// that nobody can sign in to, and that is better found at startup than at the login form.
    /// </remarks>
    public async Task SeedAdministratorAsync()
    {
        var email = _administrator.Email?.Trim();
        var legacyOwner = await _userManager.FindByIdAsync(LegacyOwner.Id);

        if (string.IsNullOrEmpty(email))
        {
            if (legacyOwner is not null)
            {
                _logger.LogWarning(
                    "Data collected before users existed belongs to no one who can sign in. " +
                    "Set {Section}:Email and {Section}:Password to hand it to an administrator.",
                    AdministratorOptions.SectionName, AdministratorOptions.SectionName);
            }

            return;
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Administrator))
        {
            EnsureSucceeded(await _roleManager.CreateAsync(new IdentityRole(Roles.Administrator)), "create the administrator role");
        }

        var administrator = await _userManager.FindByEmailAsync(email);

        if (administrator is null)
        {
            var password = RequirePassword();

            administrator = legacyOwner is not null
                ? await TakeOverLegacyOwnerAsync(legacyOwner, email, password)
                : await CreateAdministratorAsync(email, password);
        }
        else if (!await _userManager.HasPasswordAsync(administrator))
        {
            // Only a takeover interrupted between its two steps leaves this behind
            EnsureSucceeded(await _userManager.AddPasswordAsync(administrator, RequirePassword()), "set the administrator's password");
        }

        if (!await _userManager.IsInRoleAsync(administrator, Roles.Administrator))
        {
            EnsureSucceeded(await _userManager.AddToRoleAsync(administrator, Roles.Administrator), "make the administrator an administrator");
        }
    }

    private string RequirePassword()
    {
        if (string.IsNullOrEmpty(_administrator.Password))
        {
            throw new InvalidOperationException(
                $"{AdministratorOptions.SectionName}:Email is set but {AdministratorOptions.SectionName}:Password is not, " +
                "and the administrator has no password yet.");
        }

        return _administrator.Password;
    }

    private async Task<ApplicationUser> CreateAdministratorAsync(string email, string password)
    {
        var administrator = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };

        EnsureSucceeded(await _userManager.CreateAsync(administrator, password), "create the administrator");

        _logger.LogInformation("Created the administrator {Email}", email);

        return administrator;
    }

    /// <summary>
    /// Turns the placeholder owner into the administrator, which gives it everything the
    /// placeholder owned.
    /// </summary>
    /// <remarks>
    /// The password is checked before anything changes, because it is the part most likely to
    /// be refused and a refusal should leave the placeholder exactly as it was. It cannot go on
    /// first, though: setting it saves the user, saving validates it, and the placeholder has
    /// no email - which the unique-email rule rejects. So the account is renamed, then given
    /// the password it has already been shown to accept.
    /// </remarks>
    private async Task<ApplicationUser> TakeOverLegacyOwnerAsync(ApplicationUser legacyOwner, string email, string password)
    {
        foreach (var validator in _userManager.PasswordValidators)
        {
            EnsureSucceeded(await validator.ValidateAsync(_userManager, legacyOwner, password), "accept the administrator's password");
        }

        legacyOwner.UserName = email;
        legacyOwner.Email = email;
        legacyOwner.EmailConfirmed = true;

        EnsureSucceeded(await _userManager.UpdateAsync(legacyOwner), "rename the placeholder owner to the administrator");
        EnsureSucceeded(await _userManager.AddPasswordAsync(legacyOwner, password), "set the administrator's password");

        _logger.LogInformation("Gave the data collected before users existed to the administrator {Email}", email);

        return legacyOwner;
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }
}
