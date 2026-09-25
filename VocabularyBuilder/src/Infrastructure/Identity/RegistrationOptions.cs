namespace VocabularyBuilder.Infrastructure.Identity;

/// <summary>
/// Who may create an account.
/// </summary>
/// <remarks>
/// Every account can spend the OpenAI key - study content and the notes import both call the
/// model - so registration is closed to anyone not listed. With nothing listed nobody can
/// register at all; the administrator is created from <see cref="AdministratorOptions"/>
/// rather than through registration, so it never needs to be on the list.
/// </remarks>
public class RegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>
    /// Addresses allowed to register, separated by commas, semicolons or whitespace. A single
    /// string rather than an array so the whole list fits in one environment variable.
    /// </summary>
    public string? AllowedEmails { get; set; }

    public bool IsAllowed(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(AllowedEmails))
        {
            return false;
        }

        return AllowedEmails
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
