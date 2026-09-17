using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// A real SQLite database for the tests that need one. Using the actual schema rather than
/// an in-memory substitute means the unique indexes carrying the idempotency guarantees
/// are genuinely exercised.
/// </summary>
public sealed class StudyTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public StudyTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

        Context.Database.EnsureCreated();
    }

    public ApplicationDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>
/// Records what was asked of the model and replies with whatever the test wants, so
/// "this word cost no call at all" is directly assertable.
/// </summary>
public class RecordingGptClient : IGptClient
{
    private readonly Func<string, string?> _reply;

    public RecordingGptClient(Func<string, string?>? reply = null) =>
        _reply = reply ?? (_ => """{"definition":"a generated definition","sentence":"A sentence using {word} here."}""");

    public List<string> Prompts { get; } = new();

    public int CallCount => Prompts.Count;

    public string LastPrompt => Prompts[^1];

    public Task<string?> SendMessageAsync(string prompt)
    {
        Prompts.Add(prompt);
        return Task.FromResult(_reply(prompt));
    }
}

/// <summary>Clock the tests drive by hand, so stale claims and intervals need no waiting.</summary>
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
