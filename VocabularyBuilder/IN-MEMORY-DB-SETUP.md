# In-Memory Database Configuration for E2E Tests

## Implementation Summary

Successfully converted E2E tests to use an in-memory SQLite database, eliminating the need for manual database setup and providing complete test isolation.

## Changes Made

### 1. Configuration Files

**`src/Web/appsettings.E2ETest.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=VocabularyBuilderE2E;Mode=Memory;Cache=Shared"
  },
  "UseInMemoryDatabase": true,
  ...
}
```

### 2. Dependency Injection (`src/Infrastructure/DependencyInjection.cs`)

Added conditional database configuration:
- **In-Memory Mode** (E2E Tests):
  - Opens one connection that stays open, only to keep the database alive (`InMemoryDatabaseKeepAlive`)
  - Registers `ApplicationDbContext` with the connection string, so each context opens a connection of its own
  - Database persists for the lifetime of the web server
  
- **File-Based Mode** (Development/Production):
  - Uses standard SQLite file-based database
  - Applies EF Core migrations normally

### 3. Database Initialization (`src/Infrastructure/Data/ApplicationDbContextInitialiser.cs`)

Added logic to detect in-memory databases:
- **In-Memory**: Uses `EnsureCreatedAsync()` (creates schema from model)
- **File-Based**: Uses `MigrateAsync()` (applies migration files)

### 4. Application Startup (`src/Web/Program.cs`)

Added automatic database initialization for E2ETest environment:
```csharp
else if (app.Environment.EnvironmentName == "E2ETest")
{
    await app.InitialiseDatabaseAsync();
}
```

## How It Works

1. **Test Starts**: Playwright launches the app with E2ETest environment
2. **Database Opens**: The keep-alive connection opens the named in-memory database
3. **Schema Created**: `EnsureCreatedAsync()` creates tables from entity models
4. **Data Seeded**: Default roles, users, and sample data are created
5. **Tests Run**: All tests use the same in-memory database instance
6. **App Stops**: Database is automatically destroyed when the process exits

## Benefits

✅ **Zero Setup**: No `dotnet ef database update` commands needed
✅ **Fast Tests**: In-memory database is significantly faster than file-based
✅ **Complete Isolation**: Each test run gets a fresh database
✅ **No Cleanup**: No database files to delete or manage
✅ **CI/CD Friendly**: No database state management in pipelines
✅ **Parallel Safe**: Each test runner gets its own in-memory database

## Technical Details

### Why a Connection per Context, and a Keep-Alive?

SQLite in-memory databases are destroyed when the last connection closes, so one connection is
held open for the application's lifetime. Nothing queries through it.

The contexts do **not** share it. This used to be a single connection handed to every context,
and it failed now and again with "database is locked": the study enrichment worker runs in a
scope of its own at the same time as requests, and `SqliteConnection` is not thread-safe. Each
context now opens its own connection to the same database, as it would to a file.
`InMemoryDatabaseConcurrencyTests` holds that in place.

### Why EnsureCreated vs Migrations?

For in-memory databases:
- Migration files reference file paths and history tables
- `EnsureCreated()` creates the schema directly from the entity model
- This is faster and doesn't require migration files to be in sync
- Perfect for test scenarios where you always want the latest schema

### Connection String

`Data Source=VocabularyBuilderE2E;Mode=Memory;Cache=Shared` is a *named, shared-cache*
in-memory database, which is what lets separate connections reach the same data. A plain
`Data Source=:memory:` would give every connection an empty database of its own, so the app
refuses to start with one when `UseInMemoryDatabase` is on.

## Running Tests

```bash
# No database setup required - just run!
npm run test:e2e

# Interactive mode
npm run test:e2e:ui

# Debug mode
npm run test:e2e:debug
```

## Comparison: Before vs After

### Before (File-Based)
```bash
# Manual steps required
dotnet ef database update --connection "Data Source=VocabularyBuilder.E2E.db"
npm run test:e2e
# Cleanup
rm VocabularyBuilder.E2E.db
```

### After (In-Memory)
```bash
# Just run - everything is automatic
npm run test:e2e
```

## Future Enhancements

- Consider adding database seeding configuration for test-specific data
- Add option to output database schema for debugging
- Implement data fixtures for common test scenarios
