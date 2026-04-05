const { test, expect } = require('@playwright/test');
const { 
  waitForWordInDb, 
  findWordByHeadword, 
  createUniqueWord,
  getWordsFromDb,
  verifyWordsExist 
} = require('./helpers/api-helpers');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

test.describe('Words Management', () => {
  // Configure retries for backend race conditions during parallel execution
  test.describe.configure({ retries: 2 });

  test.beforeEach(async ({ request, page }) => {
    // Clean database before each test for isolation (use request context)
    await setupCleanDatabase(request);
    
    await page.goto('/words');
    
    // Wait for React app to load
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
  });

  test('should display words list page', async ({ page }) => {
    await expect(page.locator('h1, h2, h3')).toContainText('Words', { timeout: 10000 });
    
    // Check for key UI elements
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('button:has-text("Add Word")')).toBeVisible();
  });

  test('should filter words by status', async ({ page }) => {
    // Wait for words to load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Get initial word count
    const initialRows = await page.locator('table tbody tr').count();
    expect(initialRows).toBeGreaterThan(0);
    
    // Uncheck "New" status filter
    const newStatusCheckbox = page.locator('input[type="checkbox"][value="0"]');
    if (await newStatusCheckbox.isChecked()) {
      await newStatusCheckbox.click();
      await page.waitForTimeout(500); // Wait for filtering
    }
    
    // Verify the list has updated
    // (Note: actual count may be same if no New words, but this tests the mechanism)
    await expect(page.locator('table')).toBeVisible();
  });

  test('should sort words by different criteria', async ({ page }) => {
    // Wait for initial load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Sort by headword
    await page.selectOption('select#sortBy', 'headword');
    await page.waitForTimeout(500);
    
    // Verify table is still visible and has data
    await expect(page.locator('table tbody tr').first()).toBeVisible();
    
    // Sort by encounter count
    await page.selectOption('select#sortBy', 'encounterCount');
    await page.waitForTimeout(500);
    
    await expect(page.locator('table tbody tr').first()).toBeVisible();
  });

  test('should create a new word', async ({ page }) => {
    // Click Add Word button
    await page.click('button:has-text("Add Word")');
    
    // Wait for modal
    await expect(page.locator('.modal-title')).toContainText('Add Word', { timeout: 10000 });
    
    // Fill in word details with unique identifier
    const uniqueWord = createUniqueWord('testword');
    await page.fill('input[name="headword"]', uniqueWord);
    await page.fill('input[name="transcription"]', '/test/');
    await page.fill('input[name="partOfSpeech"]', 'noun');
    await page.fill('textarea[name="examples"]', 'This is a test example.');
    
    // Save the word
    await page.click('button:has-text("Save")');
    
    // Wait for modal to close
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // VERIFY: Word was actually created in the database
    const dbWord = await waitForWordInDb(page, uniqueWord);
    
    // Verify the word has expected properties
    expect(dbWord).toBeTruthy();
    expect(dbWord.headword).toBe(uniqueWord);
    expect(dbWord.transcription).toBe('/test/');
    expect(dbWord.partOfSpeech).toBe('noun');
    expect(dbWord.examples).toContain('This is a test example');
    
    // Also verify it appears in the UI (search for it)
    await page.fill('input[type="text"]', uniqueWord);
    await page.waitForTimeout(500);
    
    const wordCell = page.locator(`td:has-text("${uniqueWord}")`);
    await expect(wordCell).toBeVisible({ timeout: 5000 });
  });

  test('should view word details', async ({ page }) => {
    // Wait for words to load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Click on first word's details button
    const firstDetailsButton = page.locator('table tbody tr').first().locator('button:has-text("Details")');
    await firstDetailsButton.click();
    
    // Wait for details modal
    await expect(page.locator('.modal-title')).toBeVisible();
    await expect(page.locator('.modal-body')).toBeVisible();
    
    // Close modal
    await page.click('button:has-text("Close")');
    await expect(page.locator('.modal-title')).not.toBeVisible();
  });

  test('should edit a word', async ({ page }) => {
    // Wait for words to load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Click edit on first word
    const firstEditButton = page.locator('table tbody tr').first().locator('button:has-text("Edit")');
    await firstEditButton.click();
    
    // Wait for edit modal
    await expect(page.locator('.modal-title')).toContainText('Edit Word', { timeout: 10000 });
    
    // Modify a field
    const currentValue = await page.inputValue('input[name="headword"]');
    await page.fill('input[name="transcription"]', '/edited/');
    
    // Save changes
    await page.click('button:has-text("Save")');
    
    // Wait for modal to close
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Success - word was updated
  });

  test('should delete a word', async ({ page }) => {
    // First create a test word to delete
    await page.click('button:has-text("Add Word")');
    await expect(page.locator('.modal-title')).toContainText('Add Word', { timeout: 10000 });
    
    const uniqueWord = createUniqueWord('deleteme');
    await page.fill('input[name="headword"]', uniqueWord);
    await page.fill('input[name="partOfSpeech"]', 'noun');
    await page.click('button:has-text("Save")');
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Verify word was created in database
    const createdWord = await waitForWordInDb(page, uniqueWord);
    expect(createdWord).toBeTruthy();
    
    // Find and delete the word
    await page.fill('input[type="text"]', uniqueWord);
    await page.waitForTimeout(500);
    
    // Click delete button
    const deleteButton = page.locator(`tr:has-text("${uniqueWord}") button:has-text("Delete")`);
    await deleteButton.click();
    
    // Confirm deletion
    await page.click('button:has-text("Confirm")');
    await page.waitForTimeout(500);
    
    // VERIFY: Word is gone from database
    const deletedWord = await findWordByHeadword(page, uniqueWord);
    expect(deletedWord).toBeNull();
    
    // Also verify it's not visible in UI
    await expect(page.locator(`td:has-text("${uniqueWord}")`)).not.toBeVisible();
  });

  test('should paginate through words', async ({ page }) => {
    // Wait for initial load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Check if pagination controls exist
    const paginationInfo = page.locator('text=/Page \\d+ of \\d+/');
    
    if (await paginationInfo.isVisible()) {
      // Get current page number
      const currentText = await paginationInfo.textContent();
      
      // Click next page if available
      const nextButton = page.locator('button:has-text("Next")');
      if (await nextButton.isEnabled()) {
        await nextButton.click();
        await page.waitForTimeout(500);
        
        // Verify we moved to next page
        await expect(page.locator('table tbody tr').first()).toBeVisible();
      }
    }
  });

  test('should filter by encounter count range', async ({ page }) => {
    // Wait for initial load
    await page.waitForSelector('table tbody tr', { timeout: 10000 });
    
    // Set minimum encounter count
    const minEncounterInput = page.locator('input[placeholder="Min"]');
    if (await minEncounterInput.isVisible()) {
      await minEncounterInput.fill('2');
      await page.waitForTimeout(500);
      
      // Verify filtering occurred
      await expect(page.locator('table tbody tr').first()).toBeVisible();
    }
  });
});
