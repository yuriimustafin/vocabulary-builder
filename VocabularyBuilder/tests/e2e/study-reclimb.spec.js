const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, rungOf, seedWords, seedCard, getQueue, submitReview, cardFor, advanceClock, getCard,
  correctAnswer
} = require('./helpers/study-helpers');
const { advanceToDue } = require('./helpers/study-helpers');

/**
 * Falling back down the ladder.
 *
 * A word that starts giving trouble should meet its earlier, easier exercises again over
 * the following days rather than being left at a level it keeps failing.
 */
test.describe('Re-climbing after a failure', () => {
  test.describe.configure({ mode: 'serial' });

  const words = () => Array.from({ length: 8 }, (_, i) => `rc${String(i).padStart(2, '0')}`);
  const cloze = rungOf(ExerciseType.ContextToWordRecall);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, words());
  });

  test('failing cloze drops the word one level', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc00', rung: cloze, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc00');
    expect(card.exercise.type).toBe(ExerciseType.ContextToWordRecall);

    await submitReview(request, card, { selfGrade: 1 });

    const after = await getCard(request, 'rc00');
    expect(after.rung).toBe(cloze - 1);
    expect(after.rungStreak).toBe(0);
  });

  test('the word walks back up over the following days', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc00', rung: cloze, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const failed = cardFor(await getQueue(request), 'rc00');
    await submitReview(request, failed, { selfGrade: 1 });

    const seen = [];

    // Answering correctly works back through the level below and arrives back at cloze.
    // Not one exercise per level: the word has to earn its way up again with a run of clean
    // successes there, meeting that level's exercises in turn.
    for (let step = 0; step < 8; step++) {
      // Intervals grow from minutes to days as the card recovers, so step to whenever it
      // is actually next due rather than guessing a fixed amount.
      await advanceToDue(request, 'rc00');

      const card = cardFor(await getQueue(request), 'rc00');
      expect(card, `rc00 should be due again on step ${step + 1}`).not.toBeNull();
      seen.push(card.exercise.type);

      if (card.exercise.type === ExerciseType.ContextToWordRecall) {
        break;
      }

      await submitReview(request, card, correctAnswer(card));
    }

    const rungs = seen.map(rungOf);

    expect(rungs[0]).toBe(cloze - 1);
    expect(seen[seen.length - 1]).toBe(ExerciseType.ContextToWordRecall);

    // It only ever climbs back, never skips ahead or slips further.
    for (let i = 1; i < rungs.length; i++) {
      expect(rungs[i]).toBeGreaterThanOrEqual(rungs[i - 1]);
      expect(rungs[i] - rungs[i - 1]).toBeLessThanOrEqual(1);
    }

    expect(seen).toContain(ExerciseType.MeaningToWordScramble);
  });

  test('a correct answer straight after a failure repeats the rung rather than climbing',
    async ({ request }) => {
      await seedCard(request, {
        headword: 'rc04', rung: cloze, state: 2, intervalDays: 5,
        dueInDays: -0.1, lastReviewedDaysAgo: 1
      });

      const failed = cardFor(await getQueue(request), 'rc04');
      await submitReview(request, failed, { selfGrade: 1 });
      expect((await getCard(request, 'rc04')).rung).toBe(cloze - 1);

      await advanceToDue(request, 'rc04');
      const recovering = cardFor(await getQueue(request), 'rc04');
      await submitReview(request, recovering, correctAnswer(recovering));

      // One success is not yet enough to be made harder again: the level wants a run.
      const after = await getCard(request, 'rc04');
      expect(after.rung).toBe(cloze - 1);
      expect(after.rungStreak).toBe(1);
    });

  test('a word failed at the bottom stays at the bottom', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc01', rung: 0, state: 2, intervalDays: 3,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc01');
    await submitReview(request, card, { selfGrade: 1 });

    expect((await getCard(request, 'rc01')).rung).toBe(0);
  });

  test('a failure sends the word back through the learning steps', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc02', rung: cloze, state: 2, intervalDays: 20,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc02');
    await submitReview(request, card, { selfGrade: 1 });

    const after = await getCard(request, 'rc02');
    expect(after.state).toBe(3);        // Relearning
    expect(after.lapses).toBe(1);
    expect(after.intervalDays).toBeLessThan(20);
  });

  test('a lapse costs the word some ease', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc03', rung: cloze, state: 2, intervalDays: 10,
      easeFactor: 2.5, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc03');
    await submitReview(request, card, { selfGrade: 1 });

    expect((await getCard(request, 'rc03')).easeFactor).toBeLessThan(2.5);
  });
});
