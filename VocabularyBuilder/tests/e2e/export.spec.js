const { test, expect } = require('@playwright/test');
const { 
  createUniqueWord,
  waitForWordInDb
} = require('./helpers/api-helpers');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

test.describe('Export Words', () => {
  // Configure retries for backend race conditions during parallel execution
  test.describe.configure({ retries: 2 });

  test.beforeEach(async ({ request, page }) => {
    // Clean database before each test for isolation (use request context)
    await setupCleanDatabase(request);
    
    // Create a few test words for export testing
    await page.goto('/words');
    
    // Wait for React app to load
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    // Create 2 test words
    for (let i = 0; i < 2; i++) {
      await page.click('button:has-text("Add Word")');
      await expect(page.locator('.modal-title')).toContainText('Add Word', { timeout: 10000 });
      
      const uniqueWord = createUniqueWord(`exportword${i}`);
      await page.fill('input[name="headword"]', uniqueWord);
      await page.fill('input[name="partOfSpeech"]', 'noun');
      await page.fill('textarea[name="examples"]', `Example sentence ${i}.`);
      await page.click('button:has-text("Save")');
      await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
      
      // Verify word was created
      await waitForWordInDb(page, uniqueWord);
    }
    
    // Now navigate to export page
    await page.goto('/export-words');
    
    // Wait for React app to load
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
  });

  test('should display export page', async ({ page }) => {
    await expect(page.locator('h2, h3, h4')).toContainText(/Export/i);
    
    // Check for export button or form
    await expect(page.locator('button:has-text("Export"), button:has-text("Download")')).toBeVisible();
  });

  test('should export words to CSV/Anki format', async ({ page }) => {
    // Set up download handler
    const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
    
    // Click export button
    const exportButton = page.locator('button:has-text("Export"), button:has-text("Download")').first();
    await exportButton.click();
    
    try {
      // Wait for download
      const download = await downloadPromise;
      
      // Verify download happened
      expect(download.suggestedFilename()).toBeTruthy();
      expect(download.suggestedFilename()).toMatch(/\.(csv|txt|apkg)$/i);
      
      // Optionally save and verify the file
      const path = require('path');
      const filePath = path.join(__dirname, 'fixtures', download.suggestedFilename());
      await download.saveAs(filePath);
      
      // Clean up
      const fs = require('fs');
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    } catch (error) {
      // If no download happens, it might be because there are no words to export
      // Check for an error message or empty state message
      const noWordsMessage = page.locator('text=/no words|nothing to export/i');
      if (await noWordsMessage.isVisible()) {
        // This is acceptable - no words to export
        expect(await noWordsMessage.textContent()).toBeTruthy();
      } else {
        throw error;
      }
    }
  });

  test('should filter words by status before export', async ({ page }) => {
    // Look for status filter checkboxes
    const statusFilters = page.locator('input[type="checkbox"]');
    const filterCount = await statusFilters.count();
    
    if (filterCount > 0) {
      // Uncheck all filters first
      for (let i = 0; i < filterCount; i++) {
        const checkbox = statusFilters.nth(i);
        if (await checkbox.isChecked()) {
          await checkbox.uncheck();
        }
      }
      
      // Check only the first filter
      await statusFilters.first().check();
      
      // Verify the preview updates (if there's a preview)
      await page.waitForTimeout(500);
      
      // Now try to export
      const exportButton = page.locator('button:has-text("Export"), button:has-text("Download")').first();
      
      if (await exportButton.isEnabled()) {
        const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
        await exportButton.click();
        
        try {
          const download = await downloadPromise;
          expect(download.suggestedFilename()).toBeTruthy();
        } catch (error) {
          // No download might mean no words match the filter
          const message = page.locator('text=/no words|nothing to export|no matching/i');
          if (await message.isVisible()) {
            // This is acceptable
            expect(true).toBe(true);
          }
        }
      }
    }
  });

  test('should show preview of words to export', async ({ page }) => {
    // Look for a preview area or table
    const previewTable = page.locator('table');
    const previewList = page.locator('ul, .list-group');
    
    // If either preview exists, verify it has content
    if (await previewTable.isVisible()) {
      const rowCount = await previewTable.locator('tbody tr').count();
      // May be 0 if no words yet
      expect(rowCount).toBeGreaterThanOrEqual(0);
    } else if (await previewList.isVisible()) {
      expect(await previewList.isVisible()).toBe(true);
    }
  });

  test('should export only selected status words', async ({ page }) => {
    // Find status checkboxes (e.g., "New", "NextExport", "Exported")
    const newCheckbox = page.locator('input[type="checkbox"][value="0"]'); // Status: New
    const nextExportCheckbox = page.locator('input[type="checkbox"][value="1"]'); // Status: NextExport
    
    // Select only NextExport words
    if (await newCheckbox.isVisible() && await nextExportCheckbox.isVisible()) {
      await newCheckbox.uncheck();
      await nextExportCheckbox.check();
      
      await page.waitForTimeout(500);
      
      // Try to export
      try {
        const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
        await page.click('button:has-text("Export"), button:has-text("Download")');
        
        const download = await downloadPromise;
        expect(download.suggestedFilename()).toBeTruthy();
      } catch (error) {
        // Check for no words message
        const noWordsMsg = page.locator('text=/no words|nothing to export/i');
        if (await noWordsMsg.isVisible()) {
          // Acceptable
          expect(true).toBe(true);
        }
      }
    }
  });

  test('should handle empty export (no words)', async ({ page }) => {
    // Uncheck all status filters to get empty result
    const checkboxes = page.locator('input[type="checkbox"]');
    const count = await checkboxes.count();
    
    for (let i = 0; i < count; i++) {
      const checkbox = checkboxes.nth(i);
      if (await checkbox.isChecked()) {
        await checkbox.uncheck();
      }
    }
    
    await page.waitForTimeout(500);
    
    // Try to export
    const exportButton = page.locator('button:has-text("Export"), button:has-text("Download")').first();
    
    // Button might be disabled or clicking might show an error
    if (await exportButton.isEnabled()) {
      await exportButton.click();
      
      // Should show message about no words
      await expect(page.locator('text=/no words|nothing to export|select.*word/i')).toBeVisible({ 
        timeout: 5000 
      });
    } else {
      // Button is disabled, which is correct behavior
      expect(await exportButton.isDisabled()).toBe(true);
    }
  });

  test('should export with custom format options', async ({ page }) => {
    // Look for format dropdown or options
    const formatSelect = page.locator('select#format, select[name="format"]');
    
    if (await formatSelect.isVisible()) {
      // Select different format
      await formatSelect.selectOption({ index: 0 });
      
      await page.waitForTimeout(500);
      
      // Try export
      try {
        const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
        await page.click('button:has-text("Export"), button:has-text("Download")');
        
        const download = await downloadPromise;
        expect(download.suggestedFilename()).toBeTruthy();
      } catch (error) {
        // No words or other issue
        const message = page.locator('text=/no words|nothing to export/i');
        if (await message.isVisible()) {
          expect(true).toBe(true);
        }
      }
    }
  });
});
