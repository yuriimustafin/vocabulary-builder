const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCard, getQueue, cardFor
} = require('./helpers/study-helpers');

/**
 * Coming back to a word after a long absence.
 *
 * The probe escalates to unhinted production, with no hint offered. Retrieval with a weak
 * cue is both the larger learning gain and the only honest reading of memory - a cue there
 * would inflate a grade that is about to stretch the interval a long way.
 */
test.describe('Long-gap escalation', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `lg${String(i).padStart(2, '0')}`));
  });

  test('a long absence escalates to unhinted production', async ({ request }) => {
    await seedCard(request, {
      headword: 'lg00', rung: 3, state: 2, intervalDays: 3,
      dueInDays: -5, lastReviewedDaysAgo: 8
    });

    const card = cardFor(await getQueue(request), 'lg00');

    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordRecall);
    expect(card.rung).toBe(5);
  });

  test('the escalated probe offers no hint', async ({ request }) => {
    await seedCard(request, {
      headword: 'lg01', rung: 3, state: 2, intervalDays: 3,
      dueInDays: -5, lastReviewedDaysAgo: 8
    });

    const card = cardFor(await getQueue(request), 'lg01');

    expect(card.exercise.hint).toBeNull();
    expect(card.exercise.hintAvailable).toBe(false);
  });

  test('being well overdue escalates even when the absence is short', async ({ request }) => {
    // Two days into a one-day interval is twice as long as intended.
    await seedCard(request, {
      headword: 'lg02', rung: 3, state: 2, intervalDays: 1,
      dueInDays: -1, lastReviewedDaysAgo: 2
    });

    const card = cardFor(await getQueue(request), 'lg02');

    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordRecall);
  });

  test('a word seen on schedule keeps its own rung and its hint', async ({ request }) => {
    await seedCard(request, {
      headword: 'lg03', rung: 3, state: 2, intervalDays: 10,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'lg03');

    expect(card.exercise.type).toBe(ExerciseType.ContextToWordRecall);
    expect(card.exercise.hint).toBeTruthy();
  });

  test('a word still being learned is not escalated however long the gap', async ({ request }) => {
    await seedCard(request, {
      headword: 'lg04', rung: 1, state: 2, intervalDays: 1,
      dueInDays: -60, lastReviewedDaysAgo: 60
    });

    const card = cardFor(await getQueue(request), 'lg04');

    expect(card.rung).toBe(1);
    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningChoice);
  });

  test('escalation does not move the rung the word has actually reached', async ({ request }) => {
    await seedCard(request, {
      headword: 'lg05', rung: 3, state: 2, intervalDays: 3,
      dueInDays: -5, lastReviewedDaysAgo: 8
    });

    const card = cardFor(await getQueue(request), 'lg05');
    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordRecall);

    const { getCard } = require('./helpers/study-helpers');
    const stored = await getCard(request, 'lg05');

    expect(stored.rung, 'the escalation is for this attempt only').toBe(3);
  });
});
