const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCard, getQueue, submitReview, cardFor, advanceClock, getCard
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

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, words());
  });

  test('failing cloze drops the word two rungs', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc00', rung: 3, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc00');
    expect(card.exercise.type).toBe(ExerciseType.ContextToWordRecall);

    await submitReview(request, card, { selfGrade: 1 });

    expect((await getCard(request, 'rc00')).rung).toBe(1);
  });

  test('the word walks back up over the following days', async ({ request }) => {
    await seedCard(request, {
      headword: 'rc00', rung: 3, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const failed = cardFor(await getQueue(request), 'rc00');
    await submitReview(request, failed, { selfGrade: 1 });

    const seen = [];

    // Answering correctly walks the easier rungs and eventually arrives back at cloze.
    // Not one rung per answer: the success average dropped when the word was missed, and
    // the 85% rule holds it at a level until that record recovers, so an early success
    // repeats the rung rather than making the word harder again straight away.
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

      await submitReview(request, card, answerCorrectly(card));
    }

    expect(seen[0]).toBe(ExerciseType.WordToMeaningChoice);
    expect(seen[seen.length - 1]).toBe(ExerciseType.ContextToWordRecall);

    // It only ever climbs back, never skips ahead or slips further.
    for (let i = 1; i < seen.length; i++) {
      expect(seen[i]).toBeGreaterThanOrEqual(seen[i - 1]);
      expect(seen[i] - seen[i - 1]).toBeLessThanOrEqual(1);
    }

    expect(seen).toContain(ExerciseType.MeaningToWordChoice);
  });

  test('a correct answer straight after a failure repeats the rung rather than climbing',
    async ({ request }) => {
      await seedCard(request, {
        headword: 'rc04', rung: 3, state: 2, intervalDays: 5,
        dueInDays: -0.1, lastReviewedDaysAgo: 1
      });

      const failed = cardFor(await getQueue(request), 'rc04');
      await submitReview(request, failed, { selfGrade: 1 });
      expect((await getCard(request, 'rc04')).rung).toBe(1);

      await advanceToDue(request, 'rc04');
      const recovering = cardFor(await getQueue(request), 'rc04');
      await submitReview(request, recovering, answerCorrectly(recovering));

      // One success is not yet enough of a record to be made harder again.
      expect((await getCard(request, 'rc04')).rung).toBe(1);
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
      headword: 'rc02', rung: 3, state: 2, intervalDays: 20,
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
      headword: 'rc03', rung: 3, state: 2, intervalDays: 10,
      easeFactor: 2.5, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'rc03');
    await submitReview(request, card, { selfGrade: 1 });

    expect((await getCard(request, 'rc03')).easeFactor).toBeLessThan(2.5);
  });
});

function answerCorrectly(card) {
  const { exercise } = card;

  if (exercise.type === ExerciseType.WordToMeaningChoice) {
    return { answer: exercise.options.find(o => o.includes(card.headword)) };
  }

  if (exercise.type === ExerciseType.MeaningToWordChoice
    || exercise.type === ExerciseType.MeaningToWordScramble) {
    return { answer: card.headword };
  }

  return { selfGrade: 3 };
}
