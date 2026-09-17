const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedBareWords, seedCard, getQueue, waitForContent, cardFor
} = require('./helpers/study-helpers');

/**
 * Wrong answers come from the learner's own collection, so what the collection contains
 * decides whether a multiple-choice rung can be shown at all.
 */
test.describe('Multiple-choice distractors', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  async function serveRung(request, headword, rung) {
    await seedCard(request, {
      headword, rung, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    return cardFor(await getQueue(request), headword);
  }

  test('a healthy collection produces four options', async ({ request }) => {
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `ds${String(i).padStart(2, '0')}`));

    const card = await serveRung(request, 'ds00', 1);

    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningChoice);
    expect(card.exercise.options).toHaveLength(4);
    expect(new Set(card.exercise.options).size).toBe(4);
  });

  test('the right answer is among the options and is not marked', async ({ request }) => {
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `mk${String(i).padStart(2, '0')}`));

    const card = await serveRung(request, 'mk00', 2);

    expect(card.exercise.options).toContain('mk00');
    expect(card.exercise.answer).toBeNull();
  });

  test('too small a collection falls back to a rung that can be shown', async ({ request }) => {
    // Two words cannot furnish three wrong answers.
    await seedWords(request, ['lonely01', 'lonely02']);

    const card = await serveRung(request, 'lonely01', 1);

    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningReveal);
    expect(card.exercise.options).toBeNull();
  });

  test('the fallback stops at the nearest rung that works, not at the bottom',
    async ({ request }) => {
      // A healthy collection, but this word has no sentence of its own, so cloze cannot be
      // built and the next rung down is used rather than dropping all the way to a flashcard.
      await seedWords(request, [
        ...Array.from({ length: 10 }, (_, i) => `near${String(i).padStart(2, '0')}`),
        { headword: 'nosentence', example: null }
      ]);

      const card = await serveRung(request, 'nosentence', 3);

      expect(card.exercise.type).toBe(ExerciseType.MeaningToWordChoice);
    });

  test('rungs that need no distractors survive a thin collection', async ({ request }) => {
    // Assembling a word from its own letters needs nothing from the rest of the collection.
    await seedWords(request, ['solo01', 'solo02']);

    const card = await serveRung(request, 'solo01', 4);

    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordScramble);
    expect(card.exercise.tiles).not.toBeNull();
  });

  test('generated definitions count as distractors too', async ({ request }) => {
    // Regression: the pool read only dictionary senses, so a collection built without them
    // could never offer a wrong meaning and every word was pinned to the bottom rung.
    await seedBareWords(request, Array.from({ length: 8 }, (_, i) => `gen${String(i).padStart(2, '0')}`));
    await waitForContent(request, 8);

    const card = await serveRung(request, 'gen00', 1);

    expect(card.exercise.type).toBe(ExerciseType.WordToMeaningChoice);
    expect(card.exercise.options).toHaveLength(4);
  });

  test('a word is never offered as its own wrong answer', async ({ request }) => {
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `un${String(i).padStart(2, '0')}`));

    const card = await serveRung(request, 'un00', 2);

    expect(card.exercise.options.filter(o => o === 'un00')).toHaveLength(1);
  });
});
