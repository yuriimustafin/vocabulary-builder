const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, CardState, rungOf, seedWords, seedCard, seedCardFor, getQueue, submitReview, answerCard,
  cardFor, advanceToDue, getCard, correctAnswer
} = require('./helpers/study-helpers');

/**
 * Climbing the ladder: which exercise a word gets, and when.
 *
 * The ladder is four levels - the introduction, recognition, scaffolded production and
 * production - each with a pool of exercises. A word stays on a level until it has enough
 * clean successes there, meeting harder exercises from the pool as it goes, and leaves
 * learning once it has produced the word cleanly twice a few minutes apart.
 *
 * The first day is driven by the learning steps, so the clock is moved to each step's due
 * time rather than waiting for it.
 */
test.describe('Study ladder', () => {
  test.describe.configure({ mode: 'serial' });

  // Enough words for the multiple-choice exercises to have distractors to draw on.
  const distractors = Array.from({ length: 8 }, (_, i) => `lad${String(i).padStart(2, '0')}`);

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  /**
   * Serves the word and answers it until it leaves learning, returning the exercises it met.
   * `answerCorrectly` decides, card by card, whether to get it right.
   */
  async function studyUntilLearned(request, headword, answerCorrectly = () => true) {
    const seen = [];

    for (let step = 0; step < 16; step++) {
      await advanceToDue(request, headword, { graceMinutes: 0.1 });

      const card = cardFor(await getQueue(request), headword);
      expect(card, `${headword} should be due on step ${step + 1}`).not.toBeNull();
      seen.push(card.exercise.type);

      const correct = answerCorrectly(card);
      await answerCard(request, card, correct ? correctAnswer(card) : wrongAnswer(card));

      if ((await getCard(request, headword)).state === CardState.Review) {
        return seen;
      }
    }

    throw new Error(`${headword} never left learning: ${seen.join(', ')}`);
  }

  test('the first showing is met rather than graded', async ({ request }) => {
    await seedWords(request, ['metfirst']);

    const card = cardFor(await getQueue(request), 'metfirst');

    expect(card.isIntroduction).toBe(true);
    expect(card.exercise.answer).toBeTruthy();

    await answerCard(request, card, {});

    const after = await getCard(request, 'metfirst');
    expect(after.gradedReviews).toBe(0);
    expect(after.easeFactor).toBe(2.5);
    expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordChoice));
    expect(after.rungStreak).toBe(0);
  });

  test('a word answered cleanly climbs through every level and leaves after two productions',
    async ({ request }) => {
      await seedWords(request, [{ headword: 'remember', isMarkedForStudy: true }, ...distractors]);

      const seen = await studyUntilLearned(request, 'remember');

      expect(seen).toEqual([
        ExerciseType.WordToMeaningReveal,           // met
        ExerciseType.MeaningToWordChoice,           // recognised once
        ExerciseType.MeaningToWordSyllableScramble, // support fading over three
        ExerciseType.MeaningToWordScramble,
        ExerciseType.MeaningToWordCuedType,
        ExerciseType.ContextToWordRecall,           // produced twice, spaced
        ExerciseType.MeaningToWordType
      ]);

      const card = await getCard(request, 'remember');
      expect(card.gradedReviews).toBe(6);
      expect(card.intervalDays).toBe(1);
    });

  test('a word too short for syllables goes from the letters to typing and back',
    async ({ request }) => {
      await seedWords(request, [{ headword: 'lad00', isMarkedForStudy: true }, ...distractors.slice(1)]);

      const seen = await studyUntilLearned(request, 'lad00');

      expect(seen.slice(2, 5)).toEqual([
        ExerciseType.MeaningToWordScramble,
        ExerciseType.MeaningToWordCuedType,
        ExerciseType.MeaningToWordScramble
      ]);
    });

  test('a missed word meets the level below, not the same exercise again', async ({ request }) => {
    await seedWords(request, [{ headword: 'remember', isMarkedForStudy: true }, ...distractors]);
    let missed = false;

    const seen = await studyUntilLearned(request, 'remember', card => {
      if (card.exercise.type === ExerciseType.ContextToWordRecall && !missed) {
        missed = true;
        return false;
      }

      return true;
    });

    const missAt = seen.indexOf(ExerciseType.ContextToWordRecall);
    expect(rungOf(seen[missAt + 1])).toBe(rungOf(ExerciseType.ContextToWordRecall) - 1);
    expect(seen.length, 'the slip is paid for in more practice').toBeGreaterThan(7);

    for (let i = 1; i < seen.length; i++) {
      expect(seen[i], `no exercise twice running (step ${i + 1})`).not.toBe(seen[i - 1]);
    }
  });

  test('a quick multiple-choice answer does not skip the rest of the first session', async ({ request }) => {
    // Regression: a pick made in under three seconds was graded Easy and sent a word met a
    // minute earlier four days away.
    await seedWords(request, distractors);

    await answerCard(request, cardFor(await getQueue(request), 'lad00'), {});
    await advanceToDue(request, 'lad00');

    const card = cardFor(await getQueue(request), 'lad00');
    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordChoice);
    await submitReview(request, card, { ...correctAnswer(card), elapsedMs: 800 });

    const after = await getCard(request, 'lad00');
    expect(after.state).toBe(CardState.Learning);
    expect(after.intervalDays).toBe(0);
  });

  test('each exercise renders what it needs', async ({ request }) => {
    await seedWords(request, Array.from({ length: 8 }, (_, i) => `rung${String(i).padStart(2, '0')}`));

    const expectations = {
      [ExerciseType.WordToMeaningReveal]: card => {
        expect(card.exercise.prompt).toBe('rung00');
        expect(card.exercise.answer).toBeTruthy();
      },
      [ExerciseType.MeaningToWordChoice]: card => {
        expect(card.exercise.options).toContain('rung00');
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.ContextToWordChoice]: card => {
        expect(card.exercise.prompt).toContain('_____');
        expect(card.exercise.prompt).not.toContain('rung00');
        expect(card.exercise.options).toContain('rung00');
      },
      [ExerciseType.WordToMeaningChoice]: card => {
        expect(card.exercise.options).toHaveLength(4);
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.MeaningToWordScramble]: card => {
        expect(card.exercise.tiles.slice().sort().join('')).toBe('rung00'.split('').sort().join(''));
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.MeaningToWordCuedType]: card => {
        expect(card.exercise.letterMask).toMatch(/^r u _/);
        expect(card.exercise.letterMask).toContain('_');
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.ContextToWordRecall]: card => {
        expect(card.exercise.prompt).toContain('_____');
        expect(card.exercise.hint).toBeTruthy();
      },
      [ExerciseType.MeaningToWordType]: card => {
        expect(card.exercise.prompt).toBe('the meaning of rung00');
        expect(card.exercise.letterMask).toBeNull();
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.MeaningToWordRecall]: card => {
        expect(card.exercise.answer).toBe('rung00');
        expect(card.exercise.hint).toBeNull();
      }
    };

    for (const [type, assertion] of Object.entries(expectations)) {
      await seedCardFor(request, 'rung00', Number(type), { intervalDays: 1, lastReviewedDaysAgo: 0.5 });

      const card = cardFor(await getQueue(request), 'rung00');
      expect(card, `exercise ${type} should be served`).not.toBeNull();
      expect(card.exercise.type).toBe(Number(type));
      assertion(card);
    }
  });

  test('the syllable scramble offers the word in syllables', async ({ request }) => {
    await seedWords(request, ['chocolate', ...distractors]);
    await seedCardFor(request, 'chocolate', ExerciseType.MeaningToWordSyllableScramble, {}, { syllables: true });

    const card = cardFor(await getQueue(request), 'chocolate');

    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordSyllableScramble);
    expect(card.exercise.tiles.slice().sort()).toEqual(['cho', 'co', 'late']);
  });

  test('one clean success moves a word out of recognition', async ({ request }) => {
    await seedWords(request, ['rec00', ...distractors]);
    await seedCardFor(request, 'rec00', ExerciseType.MeaningToWordChoice);

    const card = cardFor(await getQueue(request), 'rec00');
    await submitReview(request, card, correctAnswer(card));

    const after = await getCard(request, 'rec00');
    expect(after.rung).toBe(rungOf(ExerciseType.MeaningToWordScramble));
    expect(after.rungStreak).toBe(0);
  });

  test('the scaffolded level needs three clean successes', async ({ request }) => {
    await seedWords(request, ['sca00', ...distractors]);
    await seedCard(request, {
      headword: 'sca00', rung: rungOf(ExerciseType.MeaningToWordScramble), rungStreak: 1,
      state: CardState.Review, intervalDays: 5, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'sca00');
    await submitReview(request, card, correctAnswer(card));

    const after = await getCard(request, 'sca00');
    expect(after.rung, 'two of three').toBe(rungOf(ExerciseType.MeaningToWordScramble));
    expect(after.rungStreak).toBe(2);
  });

  test('a hard answer holds the level and the streak', async ({ request }) => {
    await seedWords(request, ['hrd00', ...distractors]);

    // The cloze, which takes the learner's own grade.
    await seedCardFor(request, 'hrd00', ExerciseType.ContextToWordRecall, { intervalDays: 5 });

    const card = cardFor(await getQueue(request), 'hrd00');
    await submitReview(request, card, { selfGrade: 2 });

    const after = await getCard(request, 'hrd00');
    expect(after.rung).toBe(rungOf(ExerciseType.ContextToWordRecall));
    expect(after.rungStreak).toBe(0);
  });

  test('the exercise just asked is not asked again when the level has another', async ({ request }) => {
    await seedWords(request, ['rpt00', ...distractors]);
    await seedCardFor(request, 'rpt00', ExerciseType.MeaningToWordChoice, {
      lastExerciseType: ExerciseType.MeaningToWordChoice
    });

    const card = cardFor(await getQueue(request), 'rpt00');

    expect(card.exercise.type).toBe(ExerciseType.ContextToWordChoice);
  });
});

/** A wrong answer to whatever a card is asking. */
function wrongAnswer(card) {
  const { exercise } = card;

  if (exercise.gradingMode === 0) {
    return { selfGrade: 1 };
  }

  if (exercise.options) {
    return { answer: exercise.options.find(o => !o.includes(card.headword)) };
  }

  return { answer: 'nothing like it' };
}
