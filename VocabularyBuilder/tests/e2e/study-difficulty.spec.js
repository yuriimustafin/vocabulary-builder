const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, Ladder, rungOf, seedWords, seedCard, getQueue, submitReview, cardFor
} = require('./helpers/study-helpers');

/**
 * How much support a word gets depends on how much trouble it is giving.
 *
 * A word recalled cleanly is asked once and moves on. One showing signs of strain brings
 * its easier exercises along with it, after the graded attempt so the grade stays honest.
 */
test.describe('Difficulty tiers', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `df${String(i).padStart(2, '0')}`));
  });

  // Probed at cloze, which takes the learner's own grade.
  const probeRung = rungOf(ExerciseType.ContextToWordRecall);

  async function reviewAt(request, headword, card, grade = 3) {
    await seedCard(request, {
      headword, rung: probeRung, state: 2, intervalDays: 5, dueInDays: -0.1, lastReviewedDaysAgo: 1, ...card
    });

    const queued = cardFor(await getQueue(request), headword);
    expect(queued, `${headword} should be due`).not.toBeNull();

    return { queued, result: await submitReview(request, queued, { selfGrade: grade }) };
  }

  test('a comfortable word is asked once and moves on', async ({ request }) => {
    const { queued, result } = await reviewAt(request, 'df00', {
      easeFactor: 2.5, recentSuccessRate: 1.0, lapsesSinceRecovery: 0
    });

    expect(queued.difficulty).toBe(0);
    expect(result.followUps).toHaveLength(0);
  });

  test('a shaky word brings back the rung below', async ({ request }) => {
    const { queued, result } = await reviewAt(request, 'df01', {
      easeFactor: 2.0, recentSuccessRate: 0.8, lapsesSinceRecovery: 0
    });

    expect(queued.difficulty).toBe(1);
    expect(result.followUps).toHaveLength(1);
  });

  test('a difficult word brings back two', async ({ request }) => {
    const { queued, result } = await reviewAt(request, 'df02', {
      easeFactor: 1.6, recentSuccessRate: 0.5, lapsesSinceRecovery: 3
    });

    expect(queued.difficulty).toBe(2);
    expect(result.followUps).toHaveLength(2);
  });

  test('the follow-ups are easier than the exercise that was graded', async ({ request }) => {
    const { queued, result } = await reviewAt(request, 'df03', {
      easeFactor: 1.6, recentSuccessRate: 0.5, lapsesSinceRecovery: 3
    });

    // The support comes from the two levels below the probe, easier first: the first
    // exercise of each that is not typed - a follow-up is not marked, so there is nothing
    // for spelling it out to check against.
    expect(result.followUps.map(f => f.exercise.type)).toEqual([
      ExerciseType.MeaningToWordChoice,
      ExerciseType.MeaningToWordScramble
    ]);
    expect(queued.exercise.type).toBe(ExerciseType.ContextToWordRecall);
  });

  test('a barely-recalled answer is supported however healthy the record looks', async ({ request }) => {
    const { queued, result } = await reviewAt(request, 'df04', {
      easeFactor: 2.5, recentSuccessRate: 1.0, lapsesSinceRecovery: 0
    }, 2);

    expect(queued.difficulty, 'the record is clean').toBe(0);
    expect(result.followUps).toHaveLength(2, 'but Hard means it was only just retrieved');
  });

  test('a single lapse loses a word its comfortable standing', async ({ request }) => {
    const { queued } = await reviewAt(request, 'df05', {
      easeFactor: 2.5, recentSuccessRate: 1.0, lapsesSinceRecovery: 1
    });

    expect(queued.difficulty).toBe(1);
  });
});
