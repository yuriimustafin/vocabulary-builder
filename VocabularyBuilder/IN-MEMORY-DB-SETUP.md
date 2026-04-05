# In-Memory Database Configuration for E2E Tests

## Implementation Summary

Successfully converted E2E tests to use an in-memory SQLite database, eliminating the need for manual database setup and providing complete test isolation.

## Changes Made

### 1. Configuration Files

**`src/Web/appsettings.E2ETest.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=:memory:"
  },
  "UseInMemoryDatabase": true,
  ...
}
```

### 2. Dependency Injection (`src/Infrastructure/DependencyInjection.cs`)

Added conditional database configuration:
- **In-Memory Mode** (E2E Tests):
  - Creates a singleton `SqliteConnection` that stays open
  - Registers `ApplicationDbContext` with the shared connection
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
2. **Connection Opens**: Singleton SqliteConnection opens in-memory database
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

### Why Singleton Connection?

SQLite in-memory databases are destroyed when the last connection closes. By registering the connection as a singleton:
- It stays open for the entire application lifetime
- Multiple DbContext instances share the same connection
- The database persists across requests

### Why EnsureCreated vs Migrations?

For in-memory databases:
- Migration files reference file paths and history tables
- `EnsureCreated()` creates the schema directly from the entity model
- This is faster and doesn't require migration files to be in sync
- Perfect for test scenarios where you always want the latest schema

### Connection String

`Data Source=:memory:` creates a private in-memory database. The singleton connection pattern ensures it stays alive.

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
