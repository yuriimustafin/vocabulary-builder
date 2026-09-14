const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, seedBareWords, seedCard, getCard, getQueue
} = require('./helpers/study-helpers');

/**
 * The page itself, driven the way a learner would.
 *
 * The specs above check the rules through the API; these check that the session actually
 * renders them, records what the learner did, and moves on.
 */
test.describe('Study page', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);

    page.on('console', message => {
      if (message.type() === 'error') {
        console.log(`Browser console error: ${message.text()}`);
      }
    });
  });

  /**
   * Waits for the session to have settled into one of its states rather than for the
   * network to go quiet, which the dev server's own traffic can keep from happening.
   */
  async function openStudy(page) {
    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });

    await expect(
      page.getByTestId('study-card')
        .or(page.getByTestId('preparing-words'))
        .or(page.getByTestId('study-done'))
        .or(page.getByTestId('nothing-to-study'))
    ).toBeVisible({ timeout: 30000 });
  }

  test('a flashcard reveals its answer and takes a grade', async ({ request, page }) => {
    await seedWords(request, ['pageone']);
    await openStudy(page);

    await expect(page.getByTestId('study-card')).toBeVisible();
    await expect(page.getByTestId('exercise-prompt')).toHaveText('pageone');

    await page.getByTestId('reveal-button').click();

    await expect(page.getByTestId('exercise-answer')).toHaveText('the meaning of pageone');
    await expect(page.getByTestId('grade-bar')).toBeVisible();

    await page.getByTestId('grade-good').click();

    // Only one word was due, so the session reports itself finished.
    await expect(page.getByTestId('study-done')).toBeVisible();

    const card = await getCard(request, 'pageone');
    expect(card.gradedReviews).toBe(1);
  });

  test('the keyboard drives reveal and grading', async ({ request, page }) => {
    await seedWords(request, ['keyword01']);
    await openStudy(page);

    await page.keyboard.press('Space');
    await expect(page.getByTestId('exercise-answer')).toBeVisible();

    await page.keyboard.press('3');
    await expect(page.getByTestId('study-done')).toBeVisible();

    expect((await getCard(request, 'keyword01')).gradedReviews).toBe(1);
  });

  test('the session moves through several cards in order', async ({ request, page }) => {
    await seedWords(request, ['seqone', 'seqtwo', 'seqthree']);
    await openStudy(page);

    for (let card = 0; card < 3; card++) {
      await expect(page.getByTestId('session-progress')).toContainText(`Card ${card + 1} of 3`);
      await page.getByTestId('reveal-button').click();
      await page.getByTestId('grade-good').click();
    }

    await expect(page.getByTestId('study-done')).toBeVisible();
  });

  test('a multiple-choice card is answered by clicking an option', async ({ request, page }) => {
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `mc${String(i).padStart(2, '0')}`));
    await seedCard(request, {
      headword: 'mc00', rung: 1, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    // Keep the rest out of the way so mc00 is the only card in the session.
    const queue = await getQueue(request);
    for (const card of queue.cards.filter(c => c.headword !== 'mc00')) {
      await request.post(`/api/en/study/cards/${card.cardId}/suspend`);
    }

    await openStudy(page);

    await expect(page.getByTestId('choice-exercise')).toBeVisible();
    await expect(page.getByTestId('choice-option')).toHaveCount(4);

    await page.getByTestId('choice-option').filter({ hasText: 'the meaning of mc00' }).click();

    await expect(page.getByTestId('study-done')).toBeVisible();
    expect((await getCard(request, 'mc00')).gradedReviews).toBe(1);
  });

  test('failing a word plays out the diminishing-cues sequence', async ({ request, page }) => {
    await seedWords(request, ['failword']);
    await openStudy(page);

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-again').click();

    // First cue: one letter of the word.
    await expect(page.getByTestId('follow-up-note')).toBeVisible();
    await expect(page.getByTestId('letter-mask')).toHaveText('f _ _ _ _ _ _ _');

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-good').click();

    // Second cue: more of it, but never all.
    await expect(page.getByTestId('letter-mask')).toHaveText('f a i _ _ _ _ _');

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-good').click();

    // Then the letters as tiles.
    await expect(page.getByTestId('scramble-exercise')).toBeVisible();
  });

  test('the scramble is assembled from tiles', async ({ request, page }) => {
    await seedWords(request, ['abc']);
    await seedCard(request, {
      headword: 'abc', rung: 4, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await openStudy(page);

    await expect(page.getByTestId('scramble-exercise')).toBeVisible();

    for (const letter of ['a', 'b', 'c']) {
      await page.getByTestId('scramble-tile').filter({ hasText: letter }).first().click();
    }

    await page.getByTestId('scramble-submit').click();

    await expect(page.getByTestId('study-done')).toBeVisible();
    expect((await getCard(request, 'abc')).gradedReviews).toBe(1);
  });

  test('a cloze hides its hint until it is asked for', async ({ request, page }) => {
    await seedWords(request, ['hintword']);
    await seedCard(request, {
      headword: 'hintword', rung: 3, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await openStudy(page);

    await expect(page.getByTestId('exercise-prompt')).toContainText('_____');
    await expect(page.getByTestId('hint-text')).toHaveCount(0);

    await page.getByTestId('hint-button').click();

    await expect(page.getByTestId('hint-text')).toHaveText('the meaning of hintword');
  });

  test('the page waits while words are still being prepared', async ({ request, page }) => {
    await seedBareWords(request, ['zzfailpage']);

    await openStudy(page);

    // The only word cannot be filled in, so the session has nothing to show but says so
    // rather than failing.
    await expect(
      page.getByTestId('preparing-words').or(page.getByTestId('study-done'))
    ).toBeVisible();
  });

  test('an empty collection says so instead of looking broken', async ({ page }) => {
    await openStudy(page);

    await expect(page.getByTestId('nothing-to-study')).toBeVisible();
  });

  test('a new word is badged as new', async ({ request, page }) => {
    await seedWords(request, ['badgeword']);
    await openStudy(page);

    await expect(page.getByTestId('new-badge')).toBeVisible();
  });

  test('the nav offers the study page', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#root', { timeout: 60000 });

    await page.getByRole('link', { name: 'Study' }).click();

    await expect(page).toHaveURL(/\/study$/);
  });
});
