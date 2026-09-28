namespace VocabularyBuilder.Infrastructure.Identity;

/// <summary>
/// The owner given to everything collected before the application had users.
/// </summary>
/// <remarks>
/// The migration that introduced ownership had to put a user in every existing row, and one it
/// could create in SQL was the only candidate. It has no password, so nobody can sign in as
/// it. The administrator bootstrap takes the account over the first time an administrator is
/// configured - the same row, keeping its id - so every word it owns changes hands without a
/// single row being rewritten.
/// </remarks>
public static class LegacyOwner
{
    public const string Id = "00000000-0000-0000-0000-000000000001";

    public const string UserName = "legacy-owner";
}
