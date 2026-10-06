const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, CardState, rungOf, seedWords, seedCard, seedCardFor, getQueue, submitReview, answerCard,
  cardFor, advanceToDue, getCard, correctAnswer, waitForStudyContent
} = require('./helpers/study-helpers');

/**
 * Climbing the ladder: which exercise a word gets, and when.
 *
 * The ladder is four levels - the introduction, recognition, scaffolded production and
 * production - each with a pool of exercises. A word stays on a level until it has enough
 * clean successes there, meeting harder exercises from the pool as it goes, and leaves
 * learning once it has built the word cleanly on the scaffolded level.
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

  const firstDay = [
    ExerciseType.WordToMeaningReveal,   // met
    ExerciseType.WordToSpellingCopy,    // copied while it is in front of the learner
    ExerciseType.MeaningToWordChoice,   // recognised from its meaning, then in a sentence
    ExerciseType.ContextToWordChoice,
    ExerciseType.WordToSpellingCover    // written from memory: built once, that is the first day
  ];

  test('a word answered cleanly is copied, recognised, written once from memory, and leaves',
    async ({ request }) => {
      await seedWords(request, [{ headword: 'remember', isMarkedForStudy: true }, ...distractors]);

      const seen = await studyUntilLearned(request, 'remember');

      expect(seen).toEqual(firstDay);

      const card = await getCard(request, 'remember');
      expect(card.gradedReviews).toBe(4);
      expect(card.intervalDays).toBe(1);
      expect(card.rung, 'it keeps its level for tomorrow').toBe(rungOf(ExerciseType.WordToSpellingCover));
      expect(card.rungStreak).toBe(1);
    });

  test('with its content generated, the first day still rebuilds no sentence', async ({ request }) => {
    await seedWords(request, [{ headword: 'remember', isMarkedForStudy: true, enrich: true }, ...distractors]);
    await waitForStudyContent(request, 'remember');

    const seen = await studyUntilLearned(request, 'remember');

    expect(seen).toEqual(firstDay);
  });

  test('the sentence is rebuilt in the reviews, and not while a word is still learning', async ({ request }) => {
    await seedWords(request, [{ headword: 'remember', enrich: true }, ...distractors]);
    await waitForStudyContent(request, 'remember');

    await seedCardFor(request, 'remember', ExerciseType.TranslationToSentenceScramble, {}, { content: true });
    expect(cardFor(await getQueue(request), 'remember').exercise.type)
      .toBe(ExerciseType.TranslationToSentenceScramble);

    await seedCardFor(request, 'remember', ExerciseType.TranslationToSentenceScramble,
      { state: CardState.Learning, intervalDays: 0 }, { content: true });
    expect(cardFor(await getQueue(request), 'remember').exercise.type)
      .toBe(ExerciseType.MeaningToWordCuedType);
  });

  test('a missed word meets the level below, not the same exercise again', async ({ request }) => {
    await seedWords(request, [{ headword: 'remember', isMarkedForStudy: true }, ...distractors]);
    let missed = false;

    // Picking it from its meaning: on the first day the scaffolded level is all tolerant of a slip
    const seen = await studyUntilLearned(request, 'remember', card => {
      if (card.exercise.type === ExerciseType.MeaningToWordChoice && !missed) {
        missed = true;
        return false;
      }

      return true;
    });

    const missAt = seen.indexOf(ExerciseType.MeaningToWordChoice);
    expect(rungOf(seen[missAt + 1])).toBe(rungOf(ExerciseType.MeaningToWordChoice) - 1);
    expect(seen.length, 'the slip is paid for in more practice').toBeGreaterThan(5);

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

    const copy = cardFor(await getQueue(request), 'lad00');
    expect(copy.exercise.type).toBe(ExerciseType.WordToSpellingCopy);
    await submitReview(request, copy, correctAnswer(copy));
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
        expect(card.exercise.hint, 'the missing word\'s translation, free to ask for').toBe('the meaning of rung00');
      },
      [ExerciseType.WordToMeaningChoice]: card => {
        expect(card.exercise.options).toHaveLength(4);
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.WordToSpellingCopy]: card => {
        expect(card.exercise.prompt, 'the word is in front of the learner').toBe('rung00');
        expect(card.exercise.meaning).toBe('the meaning of rung00');
      },
      [ExerciseType.MeaningToWordScramble]: card => {
        expect(card.exercise.tiles.length).toBeGreaterThanOrEqual(3);
        expect(card.exercise.tiles.length).toBeLessThanOrEqual(4);
        expect(card.exercise.tiles.join('').split('').sort().join('')).toBe('rung00'.split('').sort().join(''));
        expect(card.exercise.answer).toBeNull();
      },
      [ExerciseType.WordToSpellingCover]: card => {
        expect(card.exercise.prompt).toBe('rung00');
        expect(card.exercise.tiles, 'four letters, so written whole').toBeNull();
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

  test('the scramble offers the word in three or four pieces, and a three-letter word in letters',
    async ({ request }) => {
      await seedWords(request, ['chocolate', 'cat', ...distractors]);

      await seedCardFor(request, 'chocolate', ExerciseType.MeaningToWordScramble);
      const long = cardFor(await getQueue(request), 'chocolate');
      expect(long.exercise.type).toBe(ExerciseType.MeaningToWordScramble);
      expect(long.exercise.tiles.slice().sort()).toEqual(['cho', 'co', 'late']);

      await seedCardFor(request, 'cat', ExerciseType.MeaningToWordScramble);
      const short = cardFor(await getQueue(request), 'cat');
      expect(short.exercise.tiles.slice().sort()).toEqual(['a', 'c', 't']);
    });

  test('three clean successes move a word out of recognition', async ({ request }) => {
    await seedWords(request, ['rec00', ...distractors]);

    // In learning, so the tries are minutes apart. A review card would be pushed weeks out
    // by each success, and moving the clock that far outlives the test's sign-in cookie.
    // Copied already, so the three asked are the ways of recognising it
    await seedCardFor(request, 'rec00', ExerciseType.MeaningToWordChoice, {
      rungStreak: 0, lastExerciseType: ExerciseType.WordToSpellingCopy,
      state: CardState.Learning, intervalDays: 0, learningStepIndex: 1
    });

    const asked = [];

    for (let i = 0; i < 3; i++) {
      await advanceToDue(request, 'rec00');
      const card = cardFor(await getQueue(request), 'rec00');
      asked.push(card.exercise.type);

      expect((await getCard(request, 'rec00')).rung, `still recognition before answer ${i + 1}`)
        .toBe(rungOf(ExerciseType.MeaningToWordChoice));

      await submitReview(request, card, correctAnswer(card));
    }

    expect(asked).toEqual([
      ExerciseType.MeaningToWordChoice, ExerciseType.ContextToWordChoice, ExerciseType.WordToMeaningChoice
    ]);

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
