const path = require('path');
const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

const HEADER =
  'term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2';

/**
 * Imports are recorded as runs of their own, with every term they were given, and every
 * action and outbound call lands in the history. These drive the two pages that show them.
 */
async function importLingQ(page, rows, importName) {
  await page.goto('/lingq-import');
  await page.fill('input[name="listName"]', importName);
  await page.setInputFiles('#lingqFileInput', {
    name: 'lingqs.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(HEADER + '\n' + rows, 'utf-8')
  });
  await page.click('button:has-text("Import Vocabulary")');
  await expect(page.locator('.alert-success')).toBeVisible({ timeout: 60000 });
}

test.describe('Imports and history', () => {
  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);
    await page.addInitScript(() => window.localStorage.setItem('language', 'fr'));
  });

  test('links from a finished import to its words', async ({ page }) => {
    await importLingQ(page, 'une randonnée,,,,,,,,,\nun pied,,,,,,,,,\nComment ça va ?,,,,,,,,,\n', 'lesson 7');

    await page.click('[data-testid="view-import-link"]');

    const details = page.locator('[data-testid="import-details"]');
    await expect(details).toBeVisible();
    await expect(page.locator('.modal-title')).toContainText('LingQ import: lesson 7');

    const words = details.locator('[data-testid="import-word"]');
    await expect(words).toHaveCount(2);
    await expect(words.filter({ hasText: 'randonnée' })).toContainText('une randonnée');
    await expect(words.filter({ hasText: 'pied' })).toContainText('New');

    await expect(details.locator('[data-testid="import-skipped"]')).toContainText('Comment ça va ?');
  });

  test('lists every import, and marks a word met again as already known', async ({ page }) => {
    await importLingQ(page, 'une main,,,,,,,,,\n', 'first');
    await importLingQ(page, 'une main,,,,,,,,,\nun pied,,,,,,,,,\n', 'second');

    await page.goto('/imports');

    const rows = page.locator('[data-testid="import-row"]');
    await expect(rows).toHaveCount(2);
    // Newest first
    await expect(rows.first()).toContainText('second');

    await rows.first().locator('[data-testid="view-import"]').click();

    const words = page.locator('[data-testid="import-word"]');
    await expect(words.filter({ hasText: 'main' })).toContainText('Already known');
    await expect(words.filter({ hasText: 'pied' })).toContainText('New');

    await page.click('[data-testid="filter-new"]');
    await expect(words).toHaveCount(1);
    await expect(words.first()).toContainText('pied');
  });

  test('opens a word from its import', async ({ page }) => {
    await importLingQ(page, 'une maison,,,,,,,,,\n', 'lesson 3');

    await page.click('[data-testid="view-import-link"]');
    await page.locator('[data-testid="import-word"] a', { hasText: 'maison' }).click();

    await expect(page).toHaveURL(/\/words\?details=\d+/);
    await expect(page.locator('[data-testid="word-imports"]')).toContainText('LingQ: lesson 3');
  });

  test('records actions and model calls in the history', async ({ page, request }) => {
    await importLingQ(page, 'une maison,,,,,,,,,\n', 'lesson 4');

    // Filling the word asks the (recorded) model, which is a call worth seeing
    const fill = await request.post('/api/fr/words/fill-dictionary');
    expect(fill.ok()).toBeTruthy();

    await page.goto('/history');

    const actions = page.locator('[data-testid="activity-action"]');
    await expect(actions.filter({ hasText: 'Import completed' })).toHaveCount(1);
    await expect(actions.filter({ hasText: 'Word filled from dictionary' })).toHaveCount(1);

    await page.selectOption('[data-testid="history-category"]', 'Imports');
    await expect(page.locator('[data-testid="activity-row"]')).toHaveCount(1);

    await page.click('[data-testid="tab-calls"]');
    const calls = page.locator('[data-testid="call-row"]');
    await expect(calls.filter({ hasText: 'Dictionary entry' }).first()).toContainText('maison');

    await calls.filter({ hasText: 'Dictionary entry' }).first().locator('[data-testid="view-call"]').click();
    await expect(page.locator('[data-testid="call-details"]')).toContainText('"maison"');

    await page.locator('.modal.show .btn-secondary', { hasText: 'Close' }).click();
    await expect(page.locator('.modal.show')).toHaveCount(0);

    // What the calls added up to, by purpose
    await page.click('[data-testid="tab-usage"]');
    await expect(page.locator('[data-testid="usage-row"]').filter({ hasText: 'Dictionary entry' })).toHaveCount(1);
  });
});
