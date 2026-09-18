using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Application.FunctionalTests;

/// <summary>
/// An in-memory SQLite database for the functional tests.
/// </summary>
/// <remarks>
/// SQLite because that is what the application runs on. The suite used to raise a SQL
/// Server container instead, and every test failed before it started: EF compares the model
/// it builds for the provider in use against the snapshot in Migrations, that snapshot is
/// generated for SQLite, and the SQL Server model differs from it in column types alone -
/// enough for Migrate() to refuse with PendingModelChangesWarning. Matching the provider
/// removes the mismatch, and drops the Docker requirement with it.
///
/// The connection is opened once and held: an ":memory:" database exists only as long as a
/// connection to it is open, so closing it would take the schema with it.
/// </remarks>
public class SqliteTestDatabase : ITestDatabase
{
    private SqliteConnection _connection = null!;

    public async Task InitialiseAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var context = new ApplicationDbContext(options);

        // Migrate rather than EnsureCreated, so the tests run against the schema the
        // migrations actually produce and a broken migration fails here rather than in
        // production
        await context.Database.MigrateAsync();
    }

    public DbConnection GetConnection()
    {
        return _connection;
    }

    /// <summary>
    /// Empties every table between tests.
    /// </summary>
    /// <remarks>
    /// The tables are read out of the database rather than listed here, so a table added
    /// later is cleared without anyone having to remember to add it - the mistake that left
    /// WordForms surviving resets in the end-to-end suite.
    /// </remarks>
    public async Task ResetAsync()
    {
        var tables = new List<string>();

        await using (var read = _connection.CreateCommand())
        {
            read.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' " +
                "AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";

            await using var reader = await read.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        await using var delete = _connection.CreateCommand();

        // Foreign keys off, because there is no order in which a full wipe satisfies them
        delete.CommandText =
            "PRAGMA foreign_keys = OFF;" +
            string.Concat(tables.Select(table => $"DELETE FROM \"{table}\";")) +
            "DELETE FROM sqlite_sequence;" +
            "PRAGMA foreign_keys = ON;";

        await delete.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
