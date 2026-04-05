# E2E Testing Best Practices Guide

## Quick Start

### Basic Test Structure with Database Verification

```javascript
const { test, expect } = require('@playwright/test');
const { 
  waitForWordInDb, 
  findWordByHeadword,
  createUniqueWord 
} = require('./helpers/api-helpers');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

test.describe('My Feature', () => {
  test.beforeEach(async ({ page }) => {
    // 1. Clean database for test isolation
    // This uses a dedicated E2E endpoint that drops/recreates the in-memory DB
    // Much faster than deleting words one-by-one
    await setupCleanDatabase(page);
    
    // 2. Navigate to your page
    await page.goto('/my-page');
    await page.waitForLoadState('networkidle');
  });

  test('should create an item', async ({ page }) => {
    // 3. Create unique test data
    const uniqueName = createUniqueWord('myitem');
    
    // 4. Perform UI action
    await page.fill('input[name="name"]', uniqueName);
    await page.click('button:has-text("Save")');
    
    // 5. Verify UI response
    await expect(page.locator('.success-message')).toBeVisible();
    
    // 6. IMPORTANT: Verify database state
    const dbItem = await waitForWordInDb(page, uniqueName);
    expect(dbItem).toBeTruthy();
    expect(dbItem.headword).toBe(uniqueName);
  });
});
```

## Common Patterns

### Pattern 1: Create and Verify

```javascript
test('should create word', async ({ page }) => {
  const word = createUniqueWord('test');
  
  // Create via UI
  await page.fill('input[name="headword"]', word);
  await page.click('button:has-text("Save")');
  
  // Verify in database
  const dbWord = await waitForWordInDb(page, word);
  expect(dbWord.headword).toBe(word);
});
```

### Pattern 2: Import and Verify Multiple Words

```javascript
test('should import multiple words', async ({ page }) => {
  const words = ['eloquent', 'vocabulary', 'test'];
  
  // Import via UI
  await page.fill('textarea', words.join('\n'));
  await page.click('button:has-text("Import")');
  
  // Verify all words in database
  for (const word of words) {
    const dbWord = await waitForWordInDb(page, word, { timeout: 15000 });
    expect(dbWord).toBeTruthy();
  }
});
```

### Pattern 3: Delete and Verify Removal

```javascript
test('should delete word', async ({ page }) => {
  const word = createUniqueWord('deleteme');
  
  // Create first
  // ... creation code ...
  await waitForWordInDb(page, word);
  
  // Delete via UI
  await page.click(`button[data-word="${word}"]`);
  await page.click('button:has-text("Confirm")');
  
  // Verify deletion in database
  const deletedWord = await findWordByHeadword(page, word);
  expect(deletedWord).toBeNull();
});
```

### Pattern 4: Verify Word Properties

```javascript
test('should save all word fields', async ({ page }) => {
  const word = createUniqueWord('complete');
  
  await page.fill('input[name="headword"]', word);
  await page.fill('input[name="transcription"]', '/test/');
  await page.fill('input[name="partOfSpeech"]', 'noun');
  await page.fill('textarea[name="examples"]', 'Example sentence.');
  await page.click('button:has-text("Save")');
  
  // Verify all properties in database
  const dbWord = await waitForWordInDb(page, word);
  expect(dbWord.headword).toBe(word);
  expect(dbWord.transcription).toBe('/test/');
  expect(dbWord.partOfSpeech).toBe('noun');
  expect(dbWord.examples).toContain('Example sentence');
});
```

### Pattern 5: Count Words (for bulk operations)

```javascript
test('should import 5 words', async ({ page }) => {
  // ... import code ...
  
  // Verify count
  const words = await verifyWordsExist(page, /testword_/, { 
    minCount: 5 
  });
  expect(words.length).toBeGreaterThanOrEqual(5);
});
```

## Helper Functions Reference

### `createUniqueWord(baseWord)`
Creates a unique word identifier to prevent test conflicts.
```javascript
const word = createUniqueWord('test');
// Returns: "test_1234567890_456"
```

### `waitForWordInDb(page, headword, options)`
Waits for a word to appear in database (handles async operations).
```javascript
const word = await waitForWordInDb(page, 'eloquent', { 
  timeout: 15000,  // Optional: default 10000ms
  lang: 'en'       // Optional: default 'en'
});
```

### `findWordByHeadword(page, headword, lang)`
Immediately searches for a word (doesn't wait).
```javascript
const word = await findWordByHeadword(page, 'test', 'en');
if (word) {
  console.log('Word exists');
} else {
  console.log('Word not found');
}
```

### `getWordsFromDb(page, options)`
Fetches all words (or filtered).
```javascript
const allWords = await getWordsFromDb(page, { 
  lang: 'en',
  searchTerm: 'test',  // Optional: filter by headword
  pageSize: 100        // Optional: default 100
});
```

### `verifyWordsExist(page, pattern, options)`
Verifies words matching a pattern exist.
```javascript
// String pattern
const words = await verifyWordsExist(page, 'testword', {
  expectedCount: 5  // Exact count
});

// Regex pattern
const words = await verifyWordsExist(page, /^test_/, {
  minCount: 3  // Minimum count
});
```

### `setupCleanDatabase(page, options)`
Cleans database before test using the E2E testing endpoint.

**How it works**: Calls `/api/e2e-testing/reset-database` which drops and recreates the in-memory database. This is **instant** compared to deleting words individually.

```javascript
await setupCleanDatabase(page, {
  skipCleanup: false  // Set true for debugging (keeps data between tests)
});
```

**Performance**: ~10ms to reset entire database vs ~500ms+ to delete words individually.

## Common Pitfalls

### ❌ DON'T: Only check UI messages
```javascript
// BAD - doesn't verify database
await page.click('button:has-text("Save")');
await expect(page.locator('.success')).toBeVisible();
```

### ✅ DO: Verify database state
```javascript
// GOOD - verifies data was actually saved
await page.click('button:has-text("Save")');
await expect(page.locator('.success')).toBeVisible();
const dbWord = await waitForWordInDb(page, uniqueWord);
expect(dbWord).toBeTruthy();
```

### ❌ DON'T: Use static test data
```javascript
// BAD - conflicts with other tests
await page.fill('input', 'testword');
```

### ✅ DO: Use unique identifiers
```javascript
// GOOD - no conflicts
const word = createUniqueWord('testword');
await page.fill('input', word);
```

### ❌ DON'T: Assume immediate database updates
```javascript
// BAD - race condition
await page.click('button:has-text("Save")');
const word = await findWordByHeadword(page, 'test');
```

### ✅ DO: Wait for database updates
```javascript
// GOOD - waits for async operation
await page.click('button:has-text("Save")');
const word = await waitForWordInDb(page, 'test', { timeout: 15000 });
```

## Debugging Tips

### Enable Database Cleanup Skip
```javascript
test.beforeEach(async ({ page }) => {
  await setupCleanDatabase(page, { skipCleanup: true });
  // ... test code
});
```

### Log Database State
```javascript
const words = await getWordsFromDb(page);
console.log('Current words:', words.map(w => w.headword));
```

### Run Single Test
```bash
npx playwright test tests/e2e/words.spec.js -g "should create word"
```

### Use UI Mode
```bash
npm run test:e2e:ui
```

## Test Isolation Checklist

- [ ] `setupCleanDatabase()` in `beforeEach`
- [ ] Use `createUniqueWord()` for test data
- [ ] Verify database state with `waitForWordInDb()`
- [ ] Don't rely solely on UI messages
- [ ] Check both presence and properties of data
- [ ] Clean up temporary files if created

## File Structure

```
tests/e2e/
├── helpers/
│   ├── api-helpers.js      # Database verification functions
│   └── db-fixtures.js      # Database cleanup fixtures
├── fixtures/
│   └── test-kindle-vocab.html  # Test data files
├── words.spec.js           # Words CRUD tests
├── bulk-import.spec.js     # Bulk import tests
├── kindle-import.spec.js   # Kindle import tests
├── export.spec.js          # Export tests
└── navigation.spec.js      # Navigation tests
```

## Questions?

Refer to:
- [E2E-TEST-IMPROVEMENTS.md](E2E-TEST-IMPROVEMENTS.md) - Detailed changes
- [E2E-TESTING-SUMMARY.md](E2E-TESTING-SUMMARY.md) - Complete setup guide
- [E2E-QUICK-REFERENCE.md](E2E-QUICK-REFERENCE.md) - Command reference
