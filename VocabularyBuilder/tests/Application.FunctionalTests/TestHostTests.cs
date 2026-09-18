using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Application.FunctionalTests;

using static Testing;

/// <summary>
/// What the test host wired up.
/// </summary>
/// <remarks>
/// These assert the harness rather than the application, because the harness failing open is
/// silent: a functional test that reaches a real dictionary or a real model still passes
/// while it has a network, and only starts costing money and flaking once someone writes a
/// test that touches one. Cheaper to fail here.
/// </remarks>
public class TestHostTests : BaseTestFixture
{
    [Test]
    public void ShouldNotResolveAModelClientThatWouldCallOut()
    {
        GetService<IGptClient>().Should().BeOfType<MockGptClient>();
    }

    [Test]
    public void ShouldNotResolveADictionaryLoaderThatWouldCallOut()
    {
        GetService<IWordReferencePageLoader>().Should().BeOfType<MockWordReferencePageLoader>();
    }

    /// <summary>
    /// Every parser the factory can route to, since it picks by language and source type and
    /// a single real one left registered is enough to reach the network.
    /// </summary>
    [Test]
    public void ShouldRegisterOnlyParsersThatStayLocal()
    {
        var parsers = GetServices<IWordReferenceParser>().ToList();

        parsers.Should().NotBeEmpty();
        parsers.Should().NotContain(p => p is OxfordParser);
        parsers.Should().Contain(p => p is MockOxfordParser);
    }

    /// <summary>
    /// The analyzer is what the notes import calls, and it is only as offline as the client
    /// underneath it.
    /// </summary>
    [Test]
    public async Task ShouldAnswerTheNotesPromptWithoutCallingOut()
    {
        var analyzer = GetService<IVocabularyAnalyzer>();

        var items = await analyzer.ExtractItemsAsync("un pas=step\nun serpent=snake", Language.French);

        items.Should().Contain("un pas");
    }

    [Test]
    public void ShouldRunOnTheTestDatabaseRatherThanAFileOnDisk()
    {
        var (isSqlite, connectionString) = WithService<ApplicationDbContext, (bool, string?)>(
            context => (context.Database.IsSqlite(), context.Database.GetConnectionString()));

        isSqlite.Should().BeTrue();
        connectionString.Should().Contain(":memory:");
    }
}
