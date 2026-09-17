const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCard, getQueue, submitReview, cardFor, getCard
} = require('./helpers/study-helpers');

/**
 * What happens after a word is missed.
 *
 * Failing a probe starts a diminishing-cues sequence: the word is re-attempted with more of
 * it showing each time until it can be produced. None of it is graded, so handing out
 * support can never inflate the card.
 */
test.describe('Diminishing cues after a failure', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `sc${String(i).padStart(2, '0')}`));
  });

  async function failAProbe(request, headword = 'sc00', rung = 3) {
    await seedCard(request, {
      headword, rung, state: 2, intervalDays: 5, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), headword);
    return submitReview(request, card, { selfGrade: 1 });
  }

  test('a failure returns the full sequence, cues shrinking', async ({ request }) => {
    const result = await failAProbe(request);

    expect(result.followUps.map(f => f.exercise.type)).toEqual([
      ExerciseType.MeaningToWordPartialLetters,
      ExerciseType.MeaningToWordPartialLetters,
      ExerciseType.MeaningToWordScramble,
      ExerciseType.WordToMeaningReveal
    ]);
  });

  test('each partial-letter step shows more of the word than the last', async ({ request }) => {
    const result = await failAProbe(request);

    const masks = result.followUps
      .filter(f => f.exercise.type === ExerciseType.MeaningToWordPartialLetters)
      .map(f => f.exercise.letterMask);

    expect(masks).toHaveLength(2);

    const revealed = masks.map(mask => mask.split(' ').filter(part => part !== '_').length);
    expect(revealed[1]).toBeGreaterThan(revealed[0]);

    // Never the whole word: that would be a reveal, not a cue.
    expect(masks[1]).toContain('_');
  });

  test('the tiles step offers exactly the letters of the word', async ({ request }) => {
    const result = await failAProbe(request);

    const tiles = result.followUps.find(f => f.exercise.type === ExerciseType.MeaningToWordScramble)
      .exercise.tiles;

    expect(tiles.slice().sort().join('')).toBe('sc00'.split('').sort().join(''));
  });

  test('the sequence ends by showing the word outright', async ({ request }) => {
    const result = await failAProbe(request);
    const last = result.followUps[result.followUps.length - 1].exercise;

    expect(last.type).toBe(ExerciseType.WordToMeaningReveal);
    expect(last.answer).toBeTruthy();
  });

  test('follow-ups do not change the interval or the ease', async ({ request }) => {
    await failAProbe(request, 'sc01');

    const before = await getCard(request, 'sc01');

    const response = await request.post('/api/en/study/follow-ups', {
      data: {
        cardId: before.id,
        attemptId: crypto.randomUUID(),
        exerciseType: ExerciseType.MeaningToWordScramble,
        elapsedMs: 3000,
        correct: true
      }
    });
    expect(response.ok()).toBeTruthy();

    const after = await getCard(request, 'sc01');

    expect(after.intervalDays).toBe(before.intervalDays);
    expect(after.easeFactor).toBe(before.easeFactor);
    expect(after.rung).toBe(before.rung);
    expect(after.recentSuccessRate).toBe(before.recentSuccessRate);
  });

  test('follow-ups are recorded separately from graded reviews', async ({ request }) => {
    await failAProbe(request, 'sc02');

    const card = await getCard(request, 'sc02');
    expect(card.gradedReviews).toBe(1);

    await request.post('/api/en/study/follow-ups', {
      data: {
        cardId: card.id,
        attemptId: crypto.randomUUID(),
        exerciseType: ExerciseType.MeaningToWordPartialLetters,
        elapsedMs: 2000,
        correct: true
      }
    });

    const after = await getCard(request, 'sc02');
    expect(after.gradedReviews, 'a follow-up is not a review').toBe(1);
    expect(after.followUps).toBe(1);
  });

  test('a word recalled cleanly gets no follow-ups at all', async ({ request }) => {
    await seedCard(request, {
      headword: 'sc03', rung: 3, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1, recentSuccessRate: 1.0, easeFactor: 2.5
    });

    const card = cardFor(await getQueue(request), 'sc03');
    const result = await submitReview(request, card, { selfGrade: 3 });

    expect(result.followUps).toHaveLength(0);
  });
});
