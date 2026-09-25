namespace VocabularyBuilder.Infrastructure.Identity;

/// <summary>
/// The administrator account, created on startup when it does not exist yet.
/// </summary>
/// <remarks>
/// The password is only used to create the account. Changing it here afterwards does nothing
/// to an account that already exists, so a password changed through the API is not quietly
/// put back on the next restart.
/// </remarks>
public class AdministratorOptions
{
    public const string SectionName = "Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }
}
