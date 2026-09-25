using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Identity;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Data;

/// <summary>
/// The in-memory database the end-to-end suite runs against, used the way the running app
/// uses it: from several scopes at once.
/// </summary>
/// <remarks>
/// The study enrichment worker reads and writes from a scope of its own while requests do the
/// same, so two contexts at a time is the normal case, not a stress case. When every context
/// shared one open connection this failed now and again with "database is locked" - rarely
/// enough in the suite to look like a flaky spec, and reliably here.
///
/// Built from AddInfrastructureServices with the E2E settings rather than by hand, so what is
/// tested is the registration the E2E host actually gets.
/// </remarks>
public class InMemoryDatabaseConcurrencyTests
{
    private const string UserId = "e2e-user";

    private ServiceProvider _services = null!;

    [SetUp]
    public async Task SetUp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A name of its own, so this database is not shared with any other test run
                ["ConnectionStrings:DefaultConnection"] = $"Data Source=e2e-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
                ["UseInMemoryDatabase"] = "true",
                ["OpenAI:UseMockMode"] = "true",
                ["Oxford:UseMockMode"] = "true",
                ["WordReference:UseMockMode"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<IUser>(_ => new FixedUser(UserId));
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>().InitialiseAsync();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser { Id = UserId, UserName = "e2e@example.com" });
        await context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
    }

    [Test]
    public async Task ShouldLetScopesReadAndWriteAtTheSameTime()
    {
        const int operations = 300;
        var failures = new ConcurrentBag<Exception>();
        var written = 0;

        await Parallel.ForEachAsync(Enumerable.Range(0, operations), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (i, cancellationToken) =>
        {
            try
            {
                using var scope = _services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                switch (i % 3)
                {
                    case 0:
                        // A request adding a word, in a transaction of its own
                        context.Words.Add(new Word
                        {
                            Headword = $"word{i}",
                            Language = Language.French,
                            WordEncounters = { new WordEncounter { Source = WordEncounterSource.Manual } }
                        });
                        await context.SaveChangesAsync(cancellationToken);
                        Interlocked.Increment(ref written);
                        break;

                    case 1:
                        // The worker sweeping for words to fill in
                        await context.Words.Include(w => w.WordEncounters).ToListAsync(cancellationToken);
                        break;

                    default:
                        // Something changing a word another scope may be reading
                        var word = await context.Words.OrderBy(w => w.Id).FirstOrDefaultAsync(cancellationToken);
                        if (word is not null)
                        {
                            word.IsMarkedForStudy = !word.IsMarkedForStudy;
                            await context.SaveChangesAsync(cancellationToken);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        });

        failures.Select(f => f.GetBaseException().Message).Distinct().Should().BeEmpty();

        // And they were all working on one database, not a private one each
        using var check = _services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Words.CountAsync())
            .Should().Be(written);
    }

    [Test]
    public async Task ShouldKeepTheDataWhileNoContextIsOpen()
    {
        using (var scope = _services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Words.Add(new Word { Headword = "maison", Language = Language.French });
            await context.SaveChangesAsync();
        }

        using (var scope = _services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Words.CountAsync())
                .Should().Be(1);
        }
    }

    private sealed record FixedUser(string? Id) : IUser;
}
