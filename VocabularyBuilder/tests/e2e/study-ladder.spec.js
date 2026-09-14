const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCard, getQueue, submitReview, answerCard, cardFor, advanceClock, getCard
} = require('./helpers/study-helpers');

/**
 * Climbing the ladder: which exercise a word gets, and when.
 *
 * The day-one sequence is driven by the learning steps, so the clock is nudged forward by
 * a minute or two between answers rather than waiting for real time.
 */
test.describe('Study ladder', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  test('the first showing is met rather than graded', async ({ request }) => {
    await seedWords(request, ['metfirst']);

    const card = cardFor(await getQueue(request), 'metfirst');

    expect(card.isIntroduction).toBe(true);
    expect(card.exercise.answer).toBeTruthy();

    await answerCard(request, card, {});

    const after = await getCard(request, 'metfirst');
    expect(after.gradedReviews).toBe(0);
    expect(after.easeFactor).toBe(2.5);
    expect(after.rung).toBe(1);
  });

  test('the first session shows three different exercise types', async ({ request }) => {
    // Enough words for the multiple-choice rungs to have distractors to draw on.
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `lad${String(i).padStart(2, '0')}`));

    const seen = [];

    for (let touch = 0; touch < 3; touch++) {
      const queue = await getQueue(request);
      const card = cardFor(queue, 'lad00');
      expect(card, `lad00 should be due on touch ${touch + 1}`).not.toBeNull();

      seen.push(card.exercise.type);

      // The first showing is met rather than graded: the learner has read the word, not
      // recalled it, so there is nothing to judge yet.
      await answerCard(request, card, answerFor(card, 3));

      // Step past the learning delay rather than waiting it out.
      await advanceClock(request, { minutes: 15 });
    }

    expect(seen).toEqual([
      ExerciseType.WordToMeaningReveal,
      ExerciseType.WordToMeaningChoice,
      ExerciseType.MeaningToWordChoice
    ]);
  });

  test('cloze arrives the day after the word is introduced', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `clo${String(i).padStart(2, '0')}`));

    for (let touch = 0; touch < 3; touch++) {
      const card = cardFor(await getQueue(request), 'clo00');
      await answerCard(request, card, answerFor(card, 3));
      await advanceClock(request, { minutes: 15 });
    }

    // Graduated, so the next appearance is a day out.
    expect(cardFor(await getQueue(request), 'clo00')).toBeNull();

    await advanceClock(request, { days: 1 });

    const card = cardFor(await getQueue(request), 'clo00');
    expect(card).not.toBeNull();
    expect(card.exercise.type).toBe(ExerciseType.ContextToWordRecall);
    expect(card.exercise.prompt).toContain('_____');
    expect(card.exercise.prompt).not.toContain('clo00');
  });

  test('each rung renders what that exercise needs', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `rung${String(i).padStart(2, '0')}`));

    const expectations = {
      [ExerciseType.WordToMeaningReveal]: card => {
        expect(card.exercise.prompt).toBe('rung00');
        expect(card.exercise.answer).toBeTruthy();
      },
      [ExerciseType.WordToMeaningChoice]: card => {
        expect(card.exercise.options).toHaveLength(4);
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.MeaningToWordChoice]: card => {
        expect(card.exercise.options).toContain('rung00');
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.ContextToWordRecall]: card => {
        expect(card.exercise.prompt).toContain('_____');
        expect(card.exercise.hint).toBeTruthy();
      },
      [ExerciseType.MeaningToWordScramble]: card => {
        expect(card.exercise.tiles.slice().sort().join('')).toBe('rung00'.split('').sort().join(''));
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.MeaningToWordRecall]: card => {
        expect(card.exercise.answer).toBe('rung00');
        expect(card.exercise.hint).toBeNull();
      }
    };

    for (const [type, assertion] of Object.entries(expectations)) {
      await seedCard(request, {
        headword: 'rung00',
        rung: Number(type),
        state: 2,
        intervalDays: 1,
        dueInDays: -0.1,
        lastReviewedDaysAgo: 0.5
      });

      const card = cardFor(await getQueue(request), 'rung00');
      expect(card, `rung ${type} should be served`).not.toBeNull();
      expect(card.exercise.type).toBe(Number(type));
      assertion(card);
    }
  });

  test('the rung does not climb while the word is being scraped through', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `scr${String(i).padStart(2, '0')}`));

    // The 85% rule: a shaky record holds the word where it is rather than making it harder.
    await seedCard(request, {
      headword: 'scr00', rung: 2, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1, recentSuccessRate: 0.4
    });

    const card = cardFor(await getQueue(request), 'scr00');
    await submitReview(request, card, answerFor(card, 3));

    const after = await getCard(request, 'scr00');
    expect(after.rung).toBe(2);
  });

  test('a strong record climbs a rung', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `str${String(i).padStart(2, '0')}`));

    await seedCard(request, {
      headword: 'str00', rung: 2, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1, recentSuccessRate: 1.0
    });

    const card = cardFor(await getQueue(request), 'str00');
    await submitReview(request, card, answerFor(card, 3));

    expect((await getCard(request, 'str00')).rung).toBe(3);
  });

  test('a hard answer holds the rung', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `hrd${String(i).padStart(2, '0')}`));

    await seedCard(request, {
      headword: 'hrd00', rung: 3, state: 2, intervalDays: 5,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'hrd00');
    await submitReview(request, card, { selfGrade: 2 });

    expect((await getCard(request, 'hrd00')).rung).toBe(3);
  });
});

/**
 * Answers a card correctly, whichever way it happens to be asking.
 * Self-graded rungs take the grade directly; the rest need the right answer.
 */
function answerFor(card, grade) {
  const { exercise } = card;

  if (exercise.type === ExerciseType.WordToMeaningChoice) {
    return { answer: exercise.options.find(o => o.includes(card.headword)) };
  }

  if (exercise.type === ExerciseType.MeaningToWordChoice) {
    return { answer: card.headword };
  }

  if (exercise.type === ExerciseType.MeaningToWordScramble) {
    return { answer: card.headword };
  }

  return { selfGrade: grade };
}

module.exports = { answerFor };
