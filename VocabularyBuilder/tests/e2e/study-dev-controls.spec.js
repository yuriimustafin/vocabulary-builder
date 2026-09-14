const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, seedCard, getQueue, getStats, getCard, introduceAllNewWords
} = require('./helpers/study-helpers');

/**
 * Throwing study progress away so a session can be tried again.
 *
 * A testing aid rather than a feature, but it deletes rows, so what it spares matters as
 * much as what it removes.
 */
test.describe('Clearing study progress', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `dv${String(i).padStart(2, '0')}`));
  });

  test('restarting today puts the words met today back to never studied', async ({ request }) => {
    await introduceAllNewWords(request);
    expect((await getStats(request)).newToday).toBeGreaterThan(0);

    const response = await request.post('/api/en/study/dev/clear-today');
    expect(response.ok()).toBeTruthy();

    const result = await response.json();
    expect(result.cardsRemoved).toBeGreaterThan(0);

    const stats = await getStats(request);
    expect(stats.newToday).toBe(0);
    expect(stats.learning).toBe(0);
  });

  test('a word first met on an earlier day is left alone', async ({ request }) => {
    await seedCard(request, {
      headword: 'dv00', rung: 3, state: 2, intervalDays: 5, dueInDays: 2, lastReviewedDaysAgo: 3
    });

    // The seed hook backdates the introduction, so this word is not part of today.
    await request.post('/api/en/study/dev/clear-today');

    expect(await getCard(request, 'dv00')).not.toBeNull();
  });

  test('clearing everything leaves nothing studied', async ({ request }) => {
    await introduceAllNewWords(request);
    await seedCard(request, {
      headword: 'dv00', rung: 3, state: 2, intervalDays: 5, dueInDays: 2, lastReviewedDaysAgo: 3
    });

    const response = await request.post('/api/en/study/dev/clear-all');
    expect(response.ok()).toBeTruthy();

    const stats = await getStats(request);
    expect(stats.learning).toBe(0);
    expect(stats.young).toBe(0);
    expect(stats.mature).toBe(0);
    expect(stats.newToday).toBe(0);
  });

  test('the words themselves survive', async ({ request }) => {
    await introduceAllNewWords(request);
    await request.post('/api/en/study/dev/clear-all');

    const words = await request.get('/api/en/words?pageSize=100').then(r => r.json());
    expect(words.totalCount).toBe(8);
  });

  test('a cleared collection can be studied again from the beginning', async ({ request }) => {
    await introduceAllNewWords(request);
    await request.post('/api/en/study/dev/clear-all');

    const queue = await getQueue(request);

    expect(queue.cards.length).toBeGreaterThan(0);
    expect(queue.cards[0].isIntroduction).toBe(true, 'every word is unmet again');
  });

  test('the buttons are on the page and restart the session', async ({ request, page }) => {
    await introduceAllNewWords(request);

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('dev-controls')).toBeVisible({ timeout: 30000 });

    await page.getByTestId('dev-clear-today').click();

    await expect(page.getByTestId('dev-clear-result')).toBeVisible();
    await expect(page.getByTestId('dev-clear-result')).toContainText('Removed');
  });
});
