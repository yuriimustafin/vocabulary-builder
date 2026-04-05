# E2E Tests with Playwright

This directory contains end-to-end (E2E) tests for the Vocabulary Builder application using Playwright.

## Overview

The test suite covers the following features:

1. **Navigation** (`navigation.spec.js`) - Home page, navigation menu, routing, language selection
2. **Words Management** (`words.spec.js`) - CRUD operations, filtering, sorting, pagination
3. **Bulk Import** (`bulk-import.spec.js`) - Import words from text lists and URLs
4. **Kindle Import** (`kindle-import.spec.js`) - Import words from Kindle vocabulary files
5. **Export** (`export.spec.js`) - Export words to CSV/Anki format

## Setup

### Prerequisites

- Node.js (v14 or later)
- .NET 9.0 SDK
- Playwright browsers installed

### Installation

From the `VocabularyBuilder` directory:

```bash
# Install dependencies
npm install

# Install Playwright browsers
npx playwright install chromium
```

Note: The E2E tests use an in-memory SQLite database that is automatically created and initialized when tests start. No manual database setup is required!

## Mock Mode for Third-Party Services

The E2E tests run in mock mode to avoid calling external APIs (OpenAI GPT and Oxford Dictionary) during tests.

### How Mocking Works

1. **Configuration**: The `appsettings.E2ETest.json` file enables mock mode:
   ```json
   {
     "OpenAI": {
       "ApiKey": "test_mode",
       "UseMockMode": true
     },
     "Oxford": {
       "UseMockMode": true
     }
   }
   ```

2. **Mock Implementations**:
   - `MockGptClient` - Returns pre-recorded GPT responses from JSON files
   - `MockOxfordParser` - Returns pre-recorded HTML from Oxford Dictionary

3. **Mock Data Location**:
   - Oxford: `src/Web/MockData/oxford/*.html`
   - GPT: `src/Web/MockData/gpt/*.json`

### Generating Mock Data

To create mock data from real API responses:

```bash
# Generate mock data (requires internet and valid API keys in appsettings.Development.json)
npm run generate:mocks
```

This script will:
- Fetch real pages from Oxford Learner's Dictionaries
- Generate realistic GPT response mocks for French words
- Save them to the `MockData` directory

**Note**: You'll need a valid OpenAI API key in `appsettings.Development.json` to generate GPT mocks. Alternatively, the script generates realistic mock responses automatically.

## Running Tests

### Run all tests

```bash
npm run test:e2e
```

### Run tests with UI mode (recommended for debugging)

```bash
npm run test:e2e:ui
```

### Run tests in headed mode (see browser)

```bash
npm run test:e2e:headed
```

### Run specific test file

```bash
npx playwright test tests/e2e/words.spec.js
```

### Run tests in debug mode

```bash
npm run test:e2e:debug
```

### Run tests against a running server

If the server is already running:

```bash
SKIP_WEBSERVER=true npm run test:e2e
```

## Project Structure

```
tests/e2e/
├── README.md                 # E2E testing documentation
├── .gitignore               # Test artifacts ignore
├── navigation.spec.js        # Navigation tests (11 tests)
├── words.spec.js             # Words CRUD tests (10 tests)
├── bulk-import.spec.js       # Bulk import tests (10 tests)
├── kindle-import.spec.js     # Kindle import tests (6 tests)
├── export.spec.js            # Export tests (6 tests)
├── fixtures/
│   └── test-helpers.js       # Test helper functions
└── mocks/
    └── generate-mock-data.js # Mock data generator
```

Note: Each test run uses a fresh in-memory database, ensuring complete isolation between test runs.

## Configuration

**`playwright.config.js`** - Main Playwright configuration:
- Base URL: `https://localhost:5001`
- Browser: Chromium
- Web server command: `dotnet run --project src/Web/Web.csproj --launch-profile E2E`
- Screenshot on failure
- Trace on first retry

**`appsettings.E2ETest.json`** - Application configuration for E2E tests:
- In-memory database: `Data Source=:memory:`
- Automatic database initialization on app startup
- Mock mode enabled for third-party services
- Reduced logging

## Launch Profile

The E2E tests use a dedicated launch profile defined in `src/Web/Properties/launchSettings.json`:

```json
{
  "E2E": {
    "commandName": "Project",
    "launchBrowser": false,
    "applicationUrl": "https://localhost:5001;http://localhost:5000",
    "environmentVariables": {
      "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES": "Microsoft.AspNetCore.SpaProxy",
      "ASPNETCORE_ENVIRONMENT": "E2ETest"
    }
  }
}
```

This profile:
- Sets environment to `E2ETest` (loads `appsettings.E2ETest.json`)
- Disaban in-memory database (automatically created on startup)
- Uses the E2E test database
- Enables mock mode for external services

## Viewing Test Reports

After running tests, view the HTML report:

```bash
npm run test:e2e:report
```

## CI/CD Integration

For continuous integration:

```bash
# Run tests with retries and single worker
CI=true npx playwright test --reporter=html,github
```

## Best Practices

1. **Isolation**: Each test should be independent and not rely on data from other tests
2. **Cleanup**: Tests that create data should clean up after themselves
3. **Waits**: Use `page.waitForLoadState('networkidle')` or specific element waits
4. **Selectors**: Prefer text-based selectors (`has-text`) for stability
5. **Mock Data**: Keep mock data realistic and up-to-date

## Troubleshooting

### Tests fail with "Target closed" or timeout errors

- Ensure the development certificate is trusted: `dotnet dev-certs https --trust`
- Check that ports 5000/5001 are not in use
- Verify the application builds successfully: `dotnet build`

### Mock data not found errors

- Run `npm run generate:mocks` to create mock data
- Check that `MockData/oxford/` and `MockData/gpt/` directories exist
- Verify `appsettings.E2ETest.json` has `UseMockMode: true`

### Tests run but app doesn't start

- Build the solution first: `dotnet build`
- Check for compilation errors: `dotnet build src/Web/Web.csproj`
- Ensure ClientApp dependencies are installed: `cd src/Web/ClientApp && npm install`

### Database-related errors

The in-memory database is automatically created on each test run. If you see database errors:
- Make sure `UseInMemoryDatabase: true` is set in `appsettings.E2ETest.json`
- Check that the application is using the E2ETest environment
- Verify no old E2E database file is interfering (delete `VocabularyBuilder.E2E.db` if it exists)

## Future Enhancements

- [ ] Add visual regression testing
- [ ] Add performance testing
- [ ] Add accessibility testing (axe-core)
- [ ] Add API-level tests
- [ ] Add mobile device testing
- [ ] Add cross-browser testing (Firefox, Safari)
