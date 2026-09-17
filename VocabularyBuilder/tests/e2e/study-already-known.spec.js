const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, seedCard, getQueue, getStats, getCard, cardFor, isolateWord
} = require('./helpers/study-helpers');

/**
 * Setting a word aside because it is already known.
 *
 * Worth acting on rather than shrugging at: studying a word you can already recognise
 * spends one of the day's places on nothing.
 */
test.describe('Already knowing a word', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  test('the word is set aside and the day gets its place back', async ({ request }) => {
    await seedWords(request, Array.from({ length: 20 }, (_, i) => `kn${String(i).padStart(2, '0')}`));

    const queue = await getQueue(request);
    const card = queue.cards[0];
    const before = await getStats(request);

    const response = await request.post(`/api/en/study/cards/${card.cardId}/known`);
    expect(response.ok()).toBeTruthy();

    const after = await getStats(request);

    expect(after.newToday, 'the place it held is handed back').toBe(before.newToday - 1);
    expect(await getCard(request, card.headword), 'and it is out of the rotation').toBeNull();
  });

  test('another word takes its place', async ({ request }) => {
    await seedWords(request, Array.from({ length: 20 }, (_, i) => `rp${String(i).padStart(2, '0')}`));

    const queue = await getQueue(request);
    const setAside = queue.cards[0].headword;

    await request.post(`/api/en/study/cards/${queue.cards[0].cardId}/known`);

    const next = await getQueue(request);

    expect(cardFor(next, setAside)).toBeNull();
    expect(next.cards.length).toBeGreaterThan(0);
  });

  test('a word set aside does not come back the next day', async ({ request }) => {
    await seedWords(request, ['onlyword']);

    const queue = await getQueue(request);
    await request.post(`/api/en/study/cards/${queue.cards[0].cardId}/known`);

    const { advanceClock } = require('./helpers/study-helpers');
    await advanceClock(request, { days: 1 });

    expect((await getQueue(request)).cards).toHaveLength(0);
  });

  test('the word itself is kept, marked as known', async ({ request }) => {
    await seedWords(request, ['keptword']);

    const queue = await getQueue(request);
    await request.post(`/api/en/study/cards/${queue.cards[0].cardId}/known`);

    const words = await request.get('/api/en/words?pageSize=100').then(r => r.json());
    const kept = words.items.find(w => w.headword === 'keptword');

    expect(kept).toBeTruthy();
    expect(kept.status, 'marked Known').toBe(4);
  });

  test('the button is offered when meeting a word, and moves on', async ({ request, page }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `pk${String(i).padStart(2, '0')}`));

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('introduction-card')).toBeVisible({ timeout: 30000 });

    const headword = await page.getByTestId('exercise-prompt').textContent();

    await page.getByTestId('introduction-known').click();

    // The session carries on with something else rather than stopping.
    await expect(page.getByTestId('exercise-prompt')).not.toHaveText(headword);
    expect(await getCard(request, headword)).toBeNull();
  });

  test('the button is not offered once a word is being tested', async ({ request, page }) => {
    // Past its first showing there is a real answer to give, so setting it aside is no
    // longer the question being asked.
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `nb${String(i).padStart(2, '0')}`));
    await seedCard(request, {
      headword: 'nb00', rung: 1, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });
    await isolateWord(request, 'nb00');

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('choice-exercise')).toBeVisible({ timeout: 30000 });

    await expect(page.getByTestId('introduction-known')).toHaveCount(0);
  });
});

/**
 * Taking back a letter while spelling.
 */
test.describe('Correcting a spelling', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);
    await seedWords(request, ['abc']);
    await seedCard(request, {
      headword: 'abc', rung: 4, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('scramble-exercise')).toBeVisible({ timeout: 30000 });
  });

  test('delete takes back only the last letter', async ({ page }) => {
    for (const letter of ['a', 'b', 'c']) {
      await page.getByTestId('scramble-tile').filter({ hasText: letter }).first().click();
    }

    await expect(page.getByTestId('placed-tile')).toHaveCount(3);

    await page.getByTestId('scramble-delete').click();

    await expect(page.getByTestId('placed-tile')).toHaveCount(2);
    await expect(page.getByTestId('scramble-tile')).toHaveCount(1);
  });

  test('start over takes back everything', async ({ page }) => {
    for (const letter of ['a', 'b']) {
      await page.getByTestId('scramble-tile').filter({ hasText: letter }).first().click();
    }

    await page.getByTestId('scramble-reset').click();

    await expect(page.getByTestId('placed-tile')).toHaveCount(0);
    await expect(page.getByTestId('scramble-tile')).toHaveCount(3);
  });

  test('correcting a letter does not cost the grade, but starting over does',
    async ({ request, page }) => {
      // A mistyped letter is part of spelling a word; clearing the lot means the spelling
      // was not known.
      await page.getByTestId('scramble-tile').filter({ hasText: 'c' }).first().click();
      await page.getByTestId('scramble-delete').click();

      for (const letter of ['a', 'b', 'c']) {
        await page.getByTestId('scramble-tile').filter({ hasText: letter }).first().click();
      }

      await page.getByTestId('scramble-submit').click();

      await expect(page.getByTestId('feedback-correct')).toBeVisible();

      const card = await getCard(request, 'abc');
      expect(card.gradedReviews).toBe(1);

      // Not asserting the grade itself: it also turns on how quickly the answer came, which
      // a browser test cannot hold still. That a correction is not treated as starting over
      // is pinned by the unit tests on the grade rules.
      expect(card.rung).toBeGreaterThanOrEqual(4, 'and the word did not slip back');
    });
});
