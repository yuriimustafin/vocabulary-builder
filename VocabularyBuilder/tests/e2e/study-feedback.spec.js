const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCard, getQueue, submitReview, cardFor, isolateWord
} = require('./helpers/study-helpers');

/**
 * What a learner is told after an automatically graded answer.
 *
 * A multiple-choice question never reveals its answer, so without this a wrong answer
 * teaches nothing: the option goes red and the session moves on. Wrong options are other
 * real words, so the one that was picked is named rather than merely marked wrong.
 */
test.describe('Answer feedback', () => {
  test.describe.configure({ mode: 'serial' });

  const pool = () => Array.from({ length: 10 }, (_, i) => `fb${String(i).padStart(2, '0')}`);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, pool());
  });

  async function serve(request, headword, rung) {
    await seedCard(request, {
      headword, rung, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    return cardFor(await getQueue(request), headword);
  }

  test('a correct answer still shows the word in full', async ({ request }) => {
    const card = await serve(request, 'fb00', 1);
    const correct = card.exercise.options.find(o => o.includes('fb00'));

    const result = await submitReview(request, card, { answer: correct });

    expect(result.feedback).not.toBeNull();
    expect(result.feedback.correct).toBe(true);
    expect(result.feedback.headword).toBe('fb00');
    expect(result.feedback.meaning).toBe('the meaning of fb00');
    expect(result.feedback.contextSentence).toContain('fb00');
    expect(result.feedback.chosen).toBeNull();
  });

  test('choosing the wrong meaning names the word it belongs to', async ({ request }) => {
    const card = await serve(request, 'fb01', 1);
    const wrong = card.exercise.options.find(o => !o.includes('fb01'));

    const result = await submitReview(request, card, { answer: wrong });

    expect(result.feedback.correct).toBe(false);
    expect(result.feedback.headword, 'the word being asked about is still shown').toBe('fb01');
    expect(result.feedback.chosen.text).toBe(wrong);

    // The meaning that was picked belongs to some other word, and it is named.
    expect(result.feedback.chosen.headword).toBeTruthy();
    expect(result.feedback.chosen.headword).not.toBe('fb01');
    expect(wrong).toContain(result.feedback.chosen.headword);
  });

  test('choosing the wrong word names what that word means', async ({ request }) => {
    const card = await serve(request, 'fb02', 2);
    const wrong = card.exercise.options.find(o => o !== 'fb02');

    const result = await submitReview(request, card, { answer: wrong });

    expect(result.feedback.correct).toBe(false);
    expect(result.feedback.headword).toBe('fb02');
    expect(result.feedback.chosen.text).toBe(wrong);
    expect(result.feedback.chosen.headword).toBe(wrong);
    expect(result.feedback.chosen.meaning).toBe(`the meaning of ${wrong}`);
  });

  test('a self-graded exercise gets no feedback', async ({ request }) => {
    // The learner revealed the answer themselves, so there is nothing left to tell them.
    const card = await serve(request, 'fb03', 0);

    const result = await submitReview(request, card, { selfGrade: 3 });

    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningReveal);
    expect(result.feedback ?? null).toBeNull();
  });

  test('a misspelling is marked without blaming another word', async ({ request }) => {
    const card = await serve(request, 'fb04', 4);

    const result = await submitReview(request, card, { answer: 'notthisword' });

    expect(result.feedback.correct).toBe(false);
    expect(result.feedback.headword).toBe('fb04');
    expect(result.feedback.chosen.text).toBe('notthisword');
    expect(result.feedback.chosen.headword ?? null).toBeNull();
  });
});

/**
 * The same thing through the page, which is where it actually matters: the option goes
 * away when clicked, so the result is the only place the word can be seen.
 */
test.describe('Answer feedback on the page', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `pf${String(i).padStart(2, '0')}`));
  });

  /** Puts one word in front of the learner and takes the rest out of the way. */
  async function studyOnly(request, page, headword, rung) {
    await seedCard(request, {
      headword, rung, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await isolateWord(request, headword);

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('study-card')).toBeVisible({ timeout: 30000 });
  }

  test('a correct choice is confirmed and the word is shown again', async ({ request, page }) => {
    await studyOnly(request, page, 'pf00', 1);

    await page.getByTestId('choice-option').filter({ hasText: 'the meaning of pf00' }).click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
    await expect(page.getByTestId('feedback-headword')).toHaveText('pf00');
    await expect(page.getByTestId('feedback-meaning')).toHaveText('the meaning of pf00');
    await expect(page.getByTestId('feedback-chosen')).toHaveCount(0);
  });

  test('a wrong choice shows the right word and what was picked instead',
    async ({ request, page }) => {
      await studyOnly(request, page, 'pf01', 1);

      const wrong = page.getByTestId('choice-option')
        .filter({ hasNotText: 'the meaning of pf01' }).first();
      const wrongText = await wrong.textContent();
      await wrong.click();

      await expect(page.getByTestId('feedback-incorrect')).toBeVisible();

      // The word that was actually being asked about.
      await expect(page.getByTestId('feedback-headword')).toHaveText('pf01');

      // And what was chosen instead, named.
      await expect(page.getByTestId('feedback-chosen-text')).toHaveText(wrongText.trim());
      await expect(page.getByTestId('feedback-chosen-owner')).toBeVisible();
    });

  test('continuing moves on to the next card', async ({ request, page }) => {
    await studyOnly(request, page, 'pf02', 1);

    await page.getByTestId('choice-option').first().click();
    await expect(page.getByTestId('answer-feedback')).toBeVisible();

    await page.getByTestId('feedback-continue').click();

    await expect(page.getByTestId('answer-feedback')).toHaveCount(0);
  });

  test('space continues from the result', async ({ request, page }) => {
    await studyOnly(request, page, 'pf03', 1);

    await page.getByTestId('choice-option').first().click();
    await expect(page.getByTestId('answer-feedback')).toBeVisible();

    await page.keyboard.press('Space');

    await expect(page.getByTestId('answer-feedback')).toHaveCount(0);
  });

  test('a self-graded card goes straight on without a result screen', async ({ request, page }) => {
    await studyOnly(request, page, 'pf04', 0);

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-good').click();

    await expect(page.getByTestId('answer-feedback')).toHaveCount(0);
  });
});
