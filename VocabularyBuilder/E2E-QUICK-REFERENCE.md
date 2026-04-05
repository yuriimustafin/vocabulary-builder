# Quick Reference: E2E Testing with In-Memory Database

## Running Tests

```bash
# Run all E2E tests
npm run test:e2e

# Run with UI (interactive mode)
npm run test:e2e:ui

# Run in headed mode (see browser)
npm run test:e2e:headed

# Debug mode
npm run test:e2e:debug

# View test report
npm run test:e2e:report
```

## First Time Setup

```bash
# 1. Install Node dependencies
npm install

# 2. Install Playwright browsers
npx playwright install chromium

# That's it! No database setup needed.
```

## How It Works

- **In-Memory Database**: Automatically created when tests start
- **Mock Mode**: External APIs (GPT, Oxford) are mocked
- **Fresh State**: Each test run gets a clean database
- **Zero Cleanup**: Database disappears when tests finish

## Test Structure

- `tests/e2e/navigation.spec.js` - Navigation and routing (11 tests)
- `tests/e2e/words.spec.js` - Words CRUD operations (10 tests)
- `tests/e2e/bulk-import.spec.js` - Bulk import feature (10 tests)
- `tests/e2e/kindle-import.spec.js` - Kindle import (6 tests)
- `tests/e2e/export.spec.js` - Export functionality (6 tests)

**Total: 43 end-to-end tests**

## Configuration

- **Environment**: E2ETest
- **Database**: In-memory SQLite (`:memory:`)
- **Base URL**: https://localhost:5001
- **Mock Data**: `src/Web/MockData/`

## Generate Mock Data

```bash
npm run generate:mocks
```

This fetches real API responses and saves them for testing.

## Documentation

- [tests/e2e/README.md](tests/e2e/README.md) - Detailed testing guide
- [E2E-TESTING-SUMMARY.md](E2E-TESTING-SUMMARY.md) - Complete implementation overview
- [IN-MEMORY-DB-SETUP.md](IN-MEMORY-DB-SETUP.md) - In-memory database technical details

## Troubleshooting

### Tests timeout or fail to start
```bash
# Trust dev certificate
dotnet dev-certs https --trust

# Verify build
dotnet build
```

### Mock data missing
```bash
# Generate mock responses
npm run generate:mocks
```

### Port conflicts
Check if ports 5000/5001 are available. Stop any running instances of the application.

## Key Benefits

✅ **No Setup** - No manual database creation
✅ **Fast** - In-memory database and mocked APIs
✅ **Isolated** - Fresh database per test run
✅ **Reliable** - No external dependencies
✅ **Developer-Friendly** - UI mode for interactive debugging
