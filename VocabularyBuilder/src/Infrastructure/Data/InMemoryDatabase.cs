using Microsoft.Data.Sqlite;

namespace VocabularyBuilder.Infrastructure.Data;

/// <summary>
/// The in-memory SQLite database the end-to-end tests run against.
/// </summary>
/// <remarks>
/// Each context opens a connection of its own to it, exactly as it would to a file. That is
/// what makes it safe to use from two places at once - the study enrichment worker's scope and
/// a request's - which a single connection shared by every context was not: SqliteConnection
/// is not thread-safe, and two scopes on one connection failed now and again with "database is
/// locked" or "cannot start a transaction within a transaction".
///
/// For separate connections to reach the same database it has to be a named, shared-cache one
/// (<c>Data Source=name;Mode=Memory;Cache=Shared</c>). A plain <c>:memory:</c> would give every
/// connection a private, empty database of its own.
/// </remarks>
public static class InMemoryDatabase
{
    /// <summary>
    /// Whether the connection string names an in-memory database of either kind.
    /// </summary>
    public static bool IsInMemory(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);

        return builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:";
    }

    /// <summary>
    /// Refuses a connection string that separate connections could not share.
    /// </summary>
    public static void EnsureShareable(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (builder.Mode != SqliteOpenMode.Memory || builder.Cache != SqliteCacheMode.Shared || builder.DataSource == ":memory:")
        {
            throw new InvalidOperationException(
                "UseInMemoryDatabase needs a named, shared-cache in-memory database, such as " +
                $"'Data Source=VocabularyBuilderE2E;Mode=Memory;Cache=Shared'. '{connectionString}' would give " +
                "every connection an empty database of its own.");
        }
    }
}

/// <summary>
/// Holds the in-memory database open for as long as the application runs.
/// </summary>
/// <remarks>
/// An in-memory database exists only while some connection to it is open, and the contexts'
/// connections open and close with every operation. This one stays open between them. Nothing
/// queries through it: a connection used by one thread at a time is the whole point.
/// </remarks>
public sealed class InMemoryDatabaseKeepAlive : IDisposable
{
    private readonly SqliteConnection _connection;

    public InMemoryDatabaseKeepAlive(string connectionString)
    {
        InMemoryDatabase.EnsureShareable(connectionString);

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
