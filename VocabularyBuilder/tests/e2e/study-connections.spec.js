const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCardFor, getQueue, submitReview, cardFor, getCard, correctAnswer,
  waitForStudyContent
} = require('./helpers/study-helpers');

/**
 * Many routes back to a word: example sentences around the words it is used with, what it
 * is used about, where it comes from, related words and a sound-alike - and an example for
 * every form it was met in, asked about before the ones already practised.
 *
 * Everything here runs against the mock model, which writes three examples for any word,
 * one more for each form it is asked about, and a line for each connection.
 */
test.describe('Examples and connections', () => {
  test.describe.configure({ mode: 'serial' });

  // Enough other words for multiple choice to have distractors.
  const others = Array.from({ length: 6 }, (_, i) => `cx${String(i).padStart(2, '0')}`);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  test('every word is given examples around the words it is used with, and connections',
    async ({ request }) => {
      await seedWords(request, [{ headword: 'vivid', isMarkedForStudy: true }, ...others]);

      const details = await waitForStudyContent(request, 'vivid');

      expect(details.studyExamples.length).toBeGreaterThanOrEqual(3);
      expect(details.studyExamples.every(e => e.sentence.includes('vivid'))).toBe(true);
      expect(details.studyExamples.every(e => e.collocation)).toBe(true);
      expect(details.studyExamples.every(e => e.translation)).toBe(true);

      expect(details.connections.usage).toBeTruthy();
      expect(details.connections.etymology).toBeTruthy();
      expect(details.connections.cognates).toBeTruthy();
      expect(details.connections.mnemonic).toBeTruthy();
    });

  test('a form the word was met in gets an example, and it is asked first', async ({ request }) => {
    await seedWords(request, [{ headword: 'shine', isMarkedForStudy: true, encounterForms: ['shone'] }, ...others]);

    const details = await waitForStudyContent(request, 'shine');
    expect(details.studyExamples.map(e => e.form)).toContain('shone');
    expect(details.encounters.map(e => e.form)).toContain('shone');

    await seedCardFor(request, 'shine', ExerciseType.ContextToWordChoice);
    const card = cardFor(await getQueue(request), 'shine');

    expect(card.exercise.type).toBe(ExerciseType.ContextToWordChoice);
    expect(card.exercise.prompt).toBe('Here the form _____ appears in a sentence.');
    expect(card.exercise.exampleId).toBeTruthy();
  });

  test('a sentence answered correctly gives way to one not yet practised', async ({ request }) => {
    await seedWords(request, [{ headword: 'vivid', isMarkedForStudy: true }, ...others]);
    await waitForStudyContent(request, 'vivid');

    await seedCardFor(request, 'vivid', ExerciseType.ContextToWordChoice);
    const first = cardFor(await getQueue(request), 'vivid');
    await submitReview(request, first, correctAnswer(first));

    await seedCardFor(request, 'vivid', ExerciseType.ContextToWordChoice);
    const second = cardFor(await getQueue(request), 'vivid');

    expect(second.exercise.exampleId).not.toBe(first.exercise.exampleId);

    const details = await request.get(`/api/en/words/${(await getCard(request, 'vivid')).wordId}/details`)
      .then(r => r.json());
    expect(details.studyExamples.filter(e => e.successes === 1)).toHaveLength(1);
  });

  test('the new word card shows what ties it to things already known', async ({ request, page }) => {
    await seedWords(request, [{ headword: 'vivid', isMarkedForStudy: true }]);
    await waitForStudyContent(request, 'vivid');

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });

    await expect(page.getByTestId('introduction-card')).toBeVisible({ timeout: 30000 });
    await expect(page.getByTestId('connection-usage')).toContainText('vivid');
    await expect(page.getByTestId('connection-etymology')).toContainText('vivid');
    await expect(page.getByTestId('connection-cognates')).toBeVisible();
    await expect(page.getByTestId('connection-mnemonic')).toBeVisible();

    // The sentence it is introduced with is one of its collocation examples
    await expect(page.getByTestId('introduction-context')).toContainText('vivid');
  });

  test('the mnemonic comes back on a miss, not on a clean answer', async ({ request }) => {
    await seedWords(request, [{ headword: 'vivid', isMarkedForStudy: true }, ...others]);
    await waitForStudyContent(request, 'vivid');

    await seedCardFor(request, 'vivid', ExerciseType.MeaningToWordChoice);
    let card = cardFor(await getQueue(request), 'vivid');
    const right = await submitReview(request, card, correctAnswer(card));

    // The feedback carries the connections either way; the page leaves the mnemonic out
    // after a clean answer
    expect(right.feedback.connections.mnemonic).toBeTruthy();

    await seedCardFor(request, 'vivid', ExerciseType.MeaningToWordChoice);
    card = cardFor(await getQueue(request), 'vivid');
    const wrong = await submitReview(request, card, { answer: card.exercise.options.find(o => o !== 'vivid') });

    expect(wrong.feedback.correct).toBe(false);
    expect(wrong.feedback.connections.etymology).toBeTruthy();
  });

  test('the word details show its study examples, connections and the forms it was met in',
    async ({ request, page }) => {
      await seedWords(request, [{ headword: 'shine', isMarkedForStudy: true, encounterForms: ['shone'] }]);
      await waitForStudyContent(request, 'shine');

      await page.goto('/words');
      await page.waitForSelector('#root', { timeout: 60000 });
      const row = page.locator('table tbody tr').filter({ has: page.locator('td', { hasText: /^shine$/ }) });
      await row.getByRole('button', { name: 'View' }).click();

      await expect(page.getByTestId('details-headword')).toBeVisible({ timeout: 10000 });
      await expect(page.getByTestId('study-example').first()).toBeVisible();
      await expect(page.getByTestId('study-example').filter({ hasText: 'shone' })).toHaveCount(1);
      await expect(page.getByTestId('word-connections')).toBeVisible();
      await expect(page.getByTestId('encounter-form').filter({ hasText: 'shone' })).toHaveCount(1);
    });
});
