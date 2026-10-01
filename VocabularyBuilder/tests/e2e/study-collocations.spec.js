const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, CardState, rungOf, seedWords, seedCard, seedCardFor, getQueue, submitReview, cardFor, getCard,
  correctAnswer, waitForStudyContent, isolateWord
} = require('./helpers/study-helpers');

/**
 * What a word goes with, its sentences rebuilt, what ties it to things already known after
 * a miss, and the day's words matched up at the end of the session.
 *
 * The mock model's collocates are "mock partner ..." and its wrong ones "mock stranger ...";
 * it translates a sentence as "Translated: " followed by the sentence.
 */
test.describe('Collocations and mistake-tolerant exercises', () => {
  test.describe.configure({ mode: 'serial' });

  const others = Array.from({ length: 6 }, (_, i) => `co${String(i).padStart(2, '0')}`);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  async function wordWithContent(request, headword = 'vivid') {
    await seedWords(request, [{ headword, isMarkedForStudy: true, enrich: true }, ...others]);
    return waitForStudyContent(request, headword);
  }

  async function serve(request, headword, type) {
    await seedCardFor(request, headword, type, {}, { content: true });
    const card = cardFor(await getQueue(request), headword);
    expect(card.exercise.type).toBe(type);
    return card;
  }

  test('what it goes with is asked among words it clearly does not', async ({ request }) => {
    await wordWithContent(request);
    const card = await serve(request, 'vivid', ExerciseType.WordToCollocatesChoice);

    expect(card.exercise.prompt).toBe('vivid');
    expect(card.exercise.options.filter(o => o.includes('partner'))).toHaveLength(3);
    expect(card.exercise.options.filter(o => o.includes('stranger'))).toHaveLength(3);

    const result = await submitReview(request, card, correctAnswer(card));

    expect(result.feedback.correct).toBe(true);
    expect(result.feedback.expectedOptions).toEqual(['mock partner one', 'mock partner two', 'mock partner three']);
    // The third clean success on recognition, which moves the word up
    const after = await getCard(request, 'vivid');
    expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordScramble));
    expect(after.rungStreak).toBe(0);
  });

  test('a greeting is never asked what it goes with', async ({ request }) => {
    await seedWords(request, [
      { headword: 'bonjour', partOfSpeech: 'interjection', isMarkedForStudy: true, enrich: true }, ...others
    ]);
    await waitForStudyContent(request, 'bonjour');

    // Where its collocates would come on recognition, the level's next exercise is asked
    await seedCardFor(request, 'bonjour', ExerciseType.WordToCollocatesChoice, {}, { content: true });
    const card = cardFor(await getQueue(request), 'bonjour');

    expect(card.exercise.type).not.toBe(ExerciseType.WordToCollocatesChoice);
    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningChoice);
  });

  test('a miss on it costs the word nothing and brings back its connections', async ({ request }) => {
    await wordWithContent(request);
    const card = await serve(request, 'vivid', ExerciseType.WordToCollocatesChoice);
    const before = await getCard(request, 'vivid');

    const result = await submitReview(request, card, { selections: ['mock stranger one', 'mock stranger two'] });

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

  test('a miss on it is not one of a learning word\'s tries', async ({ request }) => {
    // Tries count towards the cap that lets a stubborn word out of learning; a miss that
    // costs nothing must not bring that exit any closer
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.WordToCollocatesChoice,
      { state: CardState.Learning, intervalDays: 0, phaseRetrievals: 3 }, { content: true });
    const card = cardFor(await getQueue(request), 'vivid');
    expect(card.exercise.type).toBe(ExerciseType.WordToCollocatesChoice);

    const result = await submitReview(request, card, { selections: ['mock stranger one', 'mock stranger two'] });
    expect(result.tolerated).toBe(true);

    const after = await getCard(request, 'vivid');
    expect(after.state).toBe(CardState.Learning);
    expect(after.phaseRetrievals).toBe(3);
  });

  test('a slip putting the letters in order costs the word nothing', async ({ request }) => {
    await seedWords(request, ['lettre', ...others]);
    await seedCardFor(request, 'lettre', ExerciseType.MeaningToWordScramble, { state: CardState.Learning, intervalDays: 0 });
    const card = cardFor(await getQueue(request), 'lettre');
    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordScramble);
    const before = await getCard(request, 'lettre');

    const result = await submitReview(request, card, { answer: 'ertlet' });

    expect(result.tolerated).toBe(true);
    const after = await getCard(request, 'lettre');
    expect(after.rung).toBe(before.rung);
    expect(after.rungStreak).toBe(before.rungStreak);
  });

  test('a sentence rebuilt counts as practising it; a slip in the order costs nothing', async ({ request }) => {
    await wordWithContent(request);
    let card = await serve(request, 'vivid', ExerciseType.TranslationToSentenceScramble);

    expect(card.exercise.prompt).toMatch(/^Translated: /);
    expect(card.exercise.tiles).toContain('vivid');
    expect(card.exercise.exampleId).toBeTruthy();

    const wrong = await submitReview(request, card, { answer: [...card.exercise.tiles].reverse().join(' ') });
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

  test('the words it goes with are ticked and checked', async ({ request, page }) => {
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.WordToCollocatesChoice, {}, { content: true });
    await isolateWord(request, 'vivid');
    await openStudy(page);

    await expect(page.getByTestId('collocates-exercise')).toBeVisible();
    await expect(page.getByTestId('exercise-label')).toHaveText('What goes with it?');

    for (const option of ['mock partner one', 'mock partner two', 'mock partner three']) {
      await page.getByTestId('collocate-option').filter({ hasText: option }).click();
    }
    await page.getByTestId('collocates-submit').click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
    await expect(page.getByTestId('feedback-expected')).toContainText('mock partner one');
  });

  test('a sentence is rebuilt from its words', async ({ request, page }) => {
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.TranslationToSentenceScramble, {}, { content: true });
    await isolateWord(request, 'vivid');
    await openStudy(page);

    await expect(page.getByTestId('scramble-answer')).toContainText('Tap the words in order');
    const prompt = await page.getByTestId('exercise-prompt').textContent();
    const words = prompt.replace(/^Translated: /, '').replace(/[.,!?]/g, '').split(' ');
    words[0] = words[0].toLowerCase();

    for (const word of words) {
      await page.getByTestId('scramble-tile').filter({ hasText: new RegExp(`^${word}$`) }).first().click();
    }
    await page.getByTestId('scramble-submit').click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
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

  test('the translations of what it goes with are a free hint', async ({ request, page }) => {
    await wordWithContent(request);
    await seedCardFor(request, 'vivid', ExerciseType.WordToCollocatesChoice, {}, { content: true });
    await isolateWord(request, 'vivid');
    await openStudy(page);

    await expect(page.getByTestId('option-hint')).toHaveCount(0);
    await page.getByTestId('hint-button').click();
    await expect(page.getByTestId('option-hint')).toHaveCount(6);
    await expect(page.getByTestId('option-hint').first()).toContainText('meaning of mock');

    for (const partner of ['mock partner one', 'mock partner two', 'mock partner three']) {
      await page.getByTestId('collocate-option').filter({ hasText: partner }).click();
    }
    await page.getByTestId('collocates-submit').click();
    await expect(page.getByTestId('feedback-correct')).toBeVisible();

    // Its third clean answer on recognition: the hint cost nothing, so it moves up
    const after = await getCard(request, 'vivid');
    expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordScramble));
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
