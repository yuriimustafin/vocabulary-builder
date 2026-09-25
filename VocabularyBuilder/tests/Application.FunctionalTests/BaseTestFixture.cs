namespace VocabularyBuilder.Application.FunctionalTests;

using static Testing;

[TestFixture]
public abstract class BaseTestFixture
{
    /// <remarks>
    /// Every test starts signed in. The data is scoped to its owner, so with nobody signed in
    /// a handler would find nothing and could save nothing; a test about what one user cannot
    /// see of another's switches user itself.
    /// </remarks>
    [SetUp]
    public async Task TestSetUp()
    {
        await ResetState();

        await RunAsDefaultUserAsync();
    }
}
