# E2E Test Improvements: Database Verification & Test Isolation

## Overview

Updated all E2E tests to verify actual database state instead of relying only on UI toast messages. Implemented proper test isolation with database cleanup between tests.

## Key Changes

### 1. **API Helper Functions** (`tests/e2e/helpers/api-helpers.js`)

Created comprehensive helpers for database verification:

- `getWordsFromDb(page, options)` - Fetch words via API
- `findWordByHeadword(page, headword, lang)` - Find specific word
- `waitForWordInDb(page, headword, options)` - Wait for word to appear with timeout
- `resetDatabase(page)` - **NEW**: Reset entire in-memory database instantly
- `clearAllWords(page, lang)` - Legacy: Clear all words (kept for compatibility)
- `createUniqueWord(baseWord)` - Generate unique test word identifiers
- `verifyWordsExist(page, pattern, options)` - Verify words matching pattern exist

### 2. **Database Fixture** (`tests/e2e/helpers/db-fixtures.js`)

Created fixture for test isolation using efficient database reset:

- `setupCleanDatabase(page, options)` - Resets entire database via API endpoint
- `createDatabaseFixture(options)` - Global fixture configuration

**Key Optimization**: Instead of deleting words one-by-one via API, we use a dedicated E2E testing endpoint (`/api/e2e-testing/reset-database`) that drops and recreates the in-memory database. This is **much faster** and ensures a truly clean state.

### 2.1 **E2E Testing Endpoint** (`src/Web/Endpoints/E2ETestingEndpoints.cs`)

Created a dedicated endpoint for E2E testing utilities:

- **Only available in E2ETest environment** (for security)
- `POST /api/e2e-testing/reset-database` - Drops and recreates database with seed data
- `GET /api/e2e-testing/health` - Health check for test infrastructure

This endpoint uses `EnsureDeletedAsync()` + `EnsureCreatedAsync()` which is instant for in-memory databases, compared to making hundreds of DELETE requests.

### 3. **Test Updates**

#### **words.spec.js**
- ✅ Added database cleanup in `beforeEach`
- ✅ Updated "create word" test to verify database state with expected properties
- ✅ Updated "delete word" test to confirm deletion in database
- ✅ Uses unique word identifiers to prevent conflicts

**Before:**
```javascript
// Only checked toast message
await expect(page.locator('.alert-success')).toBeVisible();
```

**After:**
```javascript
// Verifies actual database state
const dbWord = await waitForWordInDb(page, uniqueWord);
expect(dbWord).toBeTruthy();
expect(dbWord.headword).toBe(uniqueWord);
expect(dbWord.partOfSpeech).toBe('noun');
```

#### **bulk-import.spec.js**
- ✅ Added database cleanup in `beforeEach`
- ✅ Updated "import from text list" to verify all words in database
- ✅ Updated "import from URLs" to verify words with Oxford Dictionary data
- ✅ Uses mocked Oxford words (eloquent, vocabulary, test, example)

**Before:**
```javascript
// Only checked for success message
await expect(page.locator('.alert-success')).toBeVisible();
```

**After:**
```javascript
// Verifies each word was actually imported
const eloquentWord = await waitForWordInDb(page, 'eloquent', { timeout: 15000 });
expect(eloquentWord).toBeTruthy();
expect(eloquentWord.partOfSpeech).toBeTruthy();
```

#### **kindle-import.spec.js**
- ✅ Fixed Kindle file format (was using tab-separated, now uses correct HTML format)
- ✅ Created proper HTML fixture matching real Kindle export structure
- ✅ Added database cleanup in `beforeEach`
- ✅ Updated all tests to use HTML fixture
- ✅ Verifies all imported words in database

**Critical Fix:**
The parser expects HTML with `.bodyContainer`, `.bookTitle`, `.noteHeading`, `.noteText` classes, NOT tab-separated values.

**Before (WRONG FORMAT):**
```javascript
const kindleContent = `en\tword1\tword\tSentence.`;
```

**After (CORRECT FORMAT):**
```html
<div class="bodyContainer">
  <div class="bookTitle">E2E Test Book</div>
  <div class="noteHeading">Highlight (yellow) - Page 1</div>
  <div class="noteText">serendipity</div>
</div>
```

Created fixture: `tests/e2e/fixtures/test-kindle-vocab.html` with 5 test words:
- serendipity
- ephemeral
- ameliorate
- ubiquitous
- meticulous

#### **export.spec.js**
- ✅ Added database cleanup in `beforeEach`
- ✅ Creates 2 test words before each test to ensure data exists for export
- ✅ Verifies words are created in database before testing export

## Benefits
Blazing Fast Test Isolation**
- Each test gets a fresh database in **milliseconds** (not seconds)
- Single API call to drop/recreate vs hundreds of DELETE requests
- No data contamination between tests

### ✅ **
### ✅ **Proper Test Isolation**
- Each test starts with clean database
- No interference between tests
- Tests can run in parallel safely

### ✅ **Actual Database Verification**
- Confirms words are actually saved, not just UI feedback
- Verifies data integrity (partOfSpeech, examples, etc.)
- Catches backend issues that UI might mask

### ✅ **More Reliable Tests**
- Unique word identifiers prevent conflicts
- Waits for database writes to complete
- Handles async operations properly

### ✅ **Better Test Data**
- Uses realistic mocked data
- Kindle fixture matches actual export format
- Oxford mock data includes proper structure

## Test Execution

All tests now follow this pattern:

1. **Setup**: Clean database
2. **Action**: Perform operation (create, import, etc.)
3. **Verify UI**: Check UI feedback
4. **Verify Database**: Confirm database state
5. **Cleanup**: Automatic (next test cleans DB)

## Running Tests

```bash
# Run all tests with database verification
npm run test:e2e

# Run specific suite
npx playwright test tests/e2e/words.spec.js

# Run with UI to see database verification
npm run test:e2e:ui
```

## Notes

- In-memory database is recreated for each test run (fast and isolated)
- No manual database setup required
- Tests verify both UI behavior and data persistence
- Kindle import now uses correct HTML format matching real exports
