const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, CardState, rungOf, seedWords, seedCard, seedCardFor, getQueue, submitReview, cardFor, getCard,
  correctAnswer, sentenceGap, tilesInOrder, waitForStudyContent, isolateWord
} = require('./helpers/study-helpers');

/**
 * Spelling the word, putting it together from its pieces, rebuilding the few words around it
 * in a sentence, what ties it to things already known after a miss, and the day's words
 * matched up at the end of the session.
 *
 * The mock model glosses each word of a sentence as "en:" and the word, and translates a
 * sentence as "Translated: " followed by the sentence.
 */
test.describe('Spelling, pieces and sentences', () => {
  test.describe.configure({ mode: 'serial' });

  const others = Array.from({ length: 6 }, (_, i) => `co${String(i).padStart(2, '0')}`);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  async function wordWithContent(request, headword = 'vivid') {
    await seedWords(request, [{ headword, isMarkedForStudy: true, enrich: true }, ...others]);
    return waitForStudyContent(request, headword);
  }

  async function serve(request, headword, type, card = {}) {
    await seedCardFor(request, headword, type, card, { content: true });
    const served = cardFor(await getQueue(request), headword);
    expect(served.exercise.type).toBe(type);
    return served;
  }

  test('a slip putting the pieces in order costs the word nothing and brings back its connections',
    async ({ request }) => {
      await wordWithContent(request);
      const card = await serve(request, 'vivid', ExerciseType.MeaningToWordScramble);
      const before = await getCard(request, 'vivid');

      const result = await submitReview(request, card, { answer: 'divid' });

      expect(result.grade).toBe(1);
      expect(result.tolerated).toBe(true);
      expect(result.feedback.note).toContain('does not count against');
      expect(result.followUps.map(f => f.exercise.type)).toEqual([ExerciseType.WordToConnectionsReveal]);

      const after = await getCard(request, 'vivid');
      expect(after.rung).toBe(before.rung);
      expect(after.rungStreak).toBe(before.rungStreak);
      expect(after.state).toBe(CardState.Review);
      expect(after.intervalDays).toBe(before.intervalDays);
      expect(after.lapses).toBe(0);
    });

  test('a tolerated miss is not one of a learning word\'s tries', async ({ request }) => {
    // Tries count towards the cap that lets a stubborn word out of learning; a miss that
    // costs nothing must not bring that exit any closer
    await seedWords(request, ['lettre', ...others]);
    await seedCardFor(request, 'lettre', ExerciseType.MeaningToWordScramble,
      { state: CardState.Learning, intervalDays: 0, phaseRetrievals: 3 });
    const card = cardFor(await getQueue(request), 'lettre');
    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordScramble);

    const result = await submitReview(request, card, { answer: 'ertlet' });
    expect(result.tolerated).toBe(true);

    const after = await getCard(request, 'lettre');
    expect(after.state).toBe(CardState.Learning);
    expect(after.phaseRetrievals).toBe(3);
  });

  test('writing it from memory forgives a miss; copying it does not', async ({ request }) => {
    await seedWords(request, ['chocolate', ...others]);

    const cover = await serve(request, 'chocolate', ExerciseType.WordToSpellingCover);
    expect(cover.exercise.prompt).toBe('chocolate');
    expect(cover.exercise.tiles).toEqual(['cho', 'co', 'late']);
    const covered = await submitReview(request, cover, { answer: 'nothing like it' });
    expect(covered.tolerated).toBe(true);

    const copy = await serve(request, 'chocolate', ExerciseType.WordToSpellingCopy);
    const copied = await submitReview(request, copy, { answer: 'nothing like it' });
    expect(copied.tolerated).toBeFalsy();
    expect(copied.grade).toBe(1);
  });

  test('a sentence is given but for the word and a few around it; a slip in their order costs nothing',
    async ({ request }) => {
      await wordWithContent(request);
      let card = await serve(request, 'vivid', ExerciseType.TranslationToSentenceScramble);
      const { exercise } = card;

      expect(exercise.tiles).toHaveLength(3);
      expect(exercise.tiles).toContain('vivid');
      expect(exercise.prompt).toBe(`${exercise.sentenceStart}_____ _____ _____${exercise.sentenceEnd}`);
      expect(exercise.sentenceStart.length + exercise.sentenceEnd.length, 'most of it is given')
        .toBeGreaterThan(sentenceGap(exercise).length);
      expect(exercise.exampleId).toBeTruthy();

      // Every part translated: the whole sentence, and each word to place
      expect(exercise.contextSentenceTranslation).toMatch(/^Translated: /);
      for (const tile of exercise.tiles) {
        expect(exercise.optionHints[tile]).toBe(`en:${tile}`);
      }

      const wrong = await submitReview(request, card, { answer: [...tilesInOrder(exercise)].reverse().join(' ') });
      expect(wrong.tolerated).toBe(true);
      expect((await getCard(request, 'vivid')).state).toBe(CardState.Review);

      card = await serve(request, 'vivid', ExerciseType.TranslationToSentenceScramble);
      const right = await submitReview(request, card, correctAnswer(card));
      expect(right.feedback.correct).toBe(true);

      const details = await request.get(`/api/en/words/${card.wordId}/details`).then(r => r.json());
      expect(details.studyExamples.filter(e => e.successes === 1)).toHaveLength(1);
    });

  // --- on the page ---------------------------------------------------------------

  async function openStudy(page) {
    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('study-card').or(page.getByTestId('study-done'))).toBeVisible({ timeout: 30000 });
  }

  test('the word is copied with it in view', async ({ request, page }) => {
    await seedWords(request, ['chocolate', ...others]);
    await seedCardFor(request, 'chocolate', ExerciseType.WordToSpellingCopy);
    await isolateWord(request, 'chocolate');
    await openStudy(page);

    await expect(page.getByTestId('exercise-label')).toHaveText('Copy the word');
    await expect(page.getByTestId('spelling-word')).toHaveText('chocolate');
    await expect(page.getByTestId('spelling-meaning')).toHaveText('the meaning of chocolate');

    await page.getByTestId('spelling-input').fill('chocolate');
    await page.getByTestId('spelling-submit').click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
  });

  test('a long word is looked at, covered and written a piece at a time', async ({ request, page }) => {
    await seedWords(request, ['chocolate', ...others]);
    await seedCardFor(request, 'chocolate', ExerciseType.WordToSpellingCover);
    await isolateWord(request, 'chocolate');
    await openStudy(page);

    await expect(page.getByTestId('exercise-label')).toHaveText('Look, cover, write');

    for (const piece of ['cho', 'co', 'late']) {
      await expect(page.getByTestId('current-piece')).toHaveText(piece);
      await expect(page.getByTestId('spelling-input')).toHaveCount(0);
      await page.getByTestId('spelling-cover').click();

      // Covered now: the piece is not on screen while it is written
      await expect(page.getByTestId('current-piece')).toHaveCount(0);
      await expect(page.getByTestId('covered-piece')).toBeVisible();
      await page.getByTestId('spelling-input').fill(piece);
      await page.getByTestId('spelling-submit').click();
    }

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
  });

  test('a sentence is rebuilt from the words around the gap, with their translations free',
    async ({ request, page }) => {
      await wordWithContent(request);
      await seedCardFor(request, 'vivid', ExerciseType.TranslationToSentenceScramble, {}, { content: true });
      const card = cardFor(await getQueue(request), 'vivid');
      await isolateWord(request, 'vivid');
      await openStudy(page);

      await expect(page.getByTestId('exercise-label')).toHaveText('Complete the sentence');
      await expect(page.getByTestId('sentence-start')).toHaveText(card.exercise.sentenceStart.trim());

      await expect(page.getByTestId('hint-text')).toHaveCount(0);
      await page.getByTestId('hint-button').click();
      await expect(page.getByTestId('hint-text')).toContainText('Translated: ');
      await expect(page.getByTestId('tile-hint')).toHaveCount(3);
      await expect(page.getByTestId('tile-hint').filter({ hasText: 'vivid' })).toContainText('en:vivid');

      for (const tile of tilesInOrder(card.exercise)) {
        await page.getByTestId('scramble-tile').filter({ hasText: new RegExp(`^${tile}$`) }).first().click();
      }
      await page.getByTestId('scramble-submit').click();

      await expect(page.getByTestId('feedback-correct')).toBeVisible();

      // The hint cost nothing: the success counts in full, and is the third on the level
      const after = await getCard(request, 'vivid');
      expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordRecall));
    });

  test('the missing word\'s meaning and the sentence\'s translation are a free hint', async ({ request, page }) => {
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.ContextToWordChoice);
    await isolateWord(request, 'vivid');
    await openStudy(page);

    await page.getByTestId('hint-button').click();
    await expect(page.getByTestId('hint-meaning')).toBeVisible();
    await expect(page.getByTestId('hint-translation')).toContainText('Translated: ');

    await page.getByTestId('choice-option').filter({ hasText: /^vivid$/ }).click();
    await expect(page.getByTestId('feedback-correct')).toBeVisible();

    // The third clean success on recognition, the hint notwithstanding, so it moves up
    const after = await getCard(request, 'vivid');
    expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordScramble));
  });

  test('a miss picking the word shows what ties it to things known, and no letters', async ({ request, page }) => {
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.MeaningToWordChoice);
    await isolateWord(request, 'vivid');
    await openStudy(page);

    await page.getByTestId('choice-option').filter({ hasNotText: /^vivid$/ }).first().click();
    await expect(page.getByTestId('feedback-incorrect')).toBeVisible();
    await expect(page.getByTestId('connection-mnemonic')).toBeVisible();
    await page.getByTestId('feedback-continue').click();

    await expect(page.getByTestId('connections-card')).toBeVisible();
    await expect(page.getByTestId('connection-mnemonic')).toContainText('mockingbird');
    await page.getByTestId('connections-continue').click();

    // Not knowing which word it was is not a spelling problem: no letter cues follow, and
    // the word comes back after its first learning step to be asked again
    await expect(page.getByTestId('connections-card')).toHaveCount(0);
    await expect(page.getByTestId('letter-mask')).toHaveCount(0);
  });

  test('at the end of the session the day\'s words are matched to their sentences', async ({ request, page }) => {
    const words = ['dr01', 'dr02', 'dr03', 'dr04', 'dr05'].map(headword => ({
      headword, example: `Yesterday ${headword} came along.`
    }));
    await seedWords(request, words);

    // Met today - a batch introduces four, so twice - and then put out of the way until
    // another day
    await getQueue(request);
    await getQueue(request);
    for (const { headword } of words) {
      await seedCard(request, { headword, state: CardState.Review, rung: rungOf(ExerciseType.MeaningToWordChoice),
        intervalDays: 2, dueInDays: 2, lastReviewedDaysAgo: 0 });
    }

    await openStudy(page);
    await expect(page.getByTestId('study-done')).toBeVisible();
    await page.getByTestId('day-review-start').click();

    await expect(page.getByTestId('day-review')).toBeVisible();
    await expect(page.getByTestId('day-review-sentence')).toHaveCount(5);
    await expect(page.getByTestId('day-review-progress')).toHaveText('Group 1 of 1');

    const sentences = page.getByTestId('day-review-sentence');
    const firstId = await sentences.first().getAttribute('data-word-id');

    // One mix-up first
    await sentences.first().click();
    await page.locator(`[data-testid="day-review-word"]:not([data-word-id="${firstId}"])`).first().click();

    for (let i = 0; i < 5; i++) {
      const sentence = page.locator('[data-testid="day-review-sentence"]:not([disabled])').first();
      const id = await sentence.getAttribute('data-word-id');
      await sentence.click();
      await page.locator(`[data-testid="day-review-word"][data-word-id="${id}"]`).click();
    }

    await page.getByTestId('day-review-next').click();

    await expect(page.getByTestId('day-review-finished')).toContainText('1 mix-up');

    // Practice only: nothing about any word's schedule moved
    expect((await getCard(request, 'dr01')).dueAtUtc).toBeTruthy();
  });
});
