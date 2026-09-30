const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  ExerciseType, seedWords, seedCardFor, getQueue, submitReview, cardFor, getCard, isolateWord
} = require('./helpers/study-helpers');

const French = 1;
const Gender = { Masculine: 1, Feminine: 2 };

/** French nouns and their neighbours, enough for multiple choice to have distractors. */
function frenchWords() {
  return [
    { headword: 'élève', language: French, partOfSpeech: 'n', gender: 3 },
    { headword: 'chaise', language: French, partOfSpeech: 'nf', gender: Gender.Feminine },
    { headword: 'poison', language: French, partOfSpeech: 'nm', gender: Gender.Masculine },
    { headword: 'poisson', language: French, partOfSpeech: 'nm', gender: Gender.Masculine },
    { headword: 'fenêtre', language: French, partOfSpeech: 'nf', gender: Gender.Feminine },
    { headword: 'maison', language: French, partOfSpeech: 'nf', gender: Gender.Feminine }
  ];
}

/**
 * Typing a word, marked leniently: the case, a missing accent, one slipped letter and a
 * leading article are forgiven in different ways, and a slip that spells another word is not.
 */
test.describe('Typed answers', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, frenchWords());
  });

  /**
   * Typing the word from its first letters - the typed exercise on the default ladder.
   * Typing it with no help at all is marked the same way, and is there to be added to the
   * production level when spelling is wanted.
   */
  async function typed(request, headword, answer, { syllables = false } = {}) {
    await seedCardFor(request, headword, ExerciseType.MeaningToWordCuedType, {}, { syllables });
    const card = cardFor(await getQueue(request, { lang: 'fr' }), headword);

    expect(card.exercise.type).toBe(ExerciseType.MeaningToWordCuedType);
    return submitReview(request, card, { answer }, 'fr');
  }

  test('the exact word counts in full', async ({ request }) => {
    const result = await typed(request, 'chaise', 'Chaise');

    expect(result.feedback.correct).toBe(true);
    expect(result.feedback.note).toBeNull();
    expect((await getCard(request, 'chaise')).rungStreak).toBe(2);
  });

  test('a missing accent is accepted but does not count towards moving on', async ({ request }) => {
    const result = await typed(request, 'fenêtre', 'fenetre', { syllables: true });

    expect(result.grade).toBe(2);
    expect(result.feedback.correct).toBe(true);
    expect(result.feedback.note).toContain('accents');
    expect((await getCard(request, 'fenêtre')).rungStreak, 'held where it was').toBe(2);
  });

  test('the right word under the wrong article is caught', async ({ request }) => {
    const result = await typed(request, 'chaise', 'le chaise');

    expect(result.grade).toBe(2);
    expect(result.feedback.note).toContain('la chaise');
  });

  test('the right article is simply accepted', async ({ request }) => {
    const result = await typed(request, 'chaise', 'la chaise');

    expect(result.feedback.correct).toBe(true);
    expect(result.feedback.note).toBeNull();
  });

  test('a slip that spells another word in the collection is wrong', async ({ request }) => {
    const result = await typed(request, 'poison', 'poisson');

    expect(result.feedback.correct).toBe(false);
    expect(result.feedback.chosen.text).toBe('poisson');
  });

  test('giving up is a miss and drops the word a level', async ({ request }) => {
    await seedCardFor(request, 'maison', ExerciseType.MeaningToWordCuedType);
    const card = cardFor(await getQueue(request, { lang: 'fr' }), 'maison');

    const result = await submitReview(request, card, { answer: '', abandoned: true }, 'fr');

    expect(result.grade).toBe(1);
    expect((await getCard(request, 'maison')).rung).toBe(1);
  });
});

/**
 * The new exercises on the page, driven the way a learner would.
 */
test.describe('New exercises on the page', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  async function openStudy(page, language = 'en') {
    await page.addInitScript(lang => window.localStorage.setItem('language', lang), language);
    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('study-card')).toBeVisible({ timeout: 30000 });
  }

  test('a word is typed and checked with enter', async ({ request, page }) => {
    await seedWords(request, ['keyboard']);
    await seedCardFor(request, 'keyboard', ExerciseType.MeaningToWordCuedType);
    await openStudy(page);

    await expect(page.getByTestId('typed-exercise')).toBeVisible();
    await expect(page.getByTestId('exercise-prompt')).toHaveText('the meaning of keyboard');
    await expect(page.getByTestId('accent-bar'), 'no accents to offer in English').toHaveCount(0);

    await page.getByTestId('typed-input').fill('keyboard');
    await page.getByTestId('typed-input').press('Enter');

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
    await expect(page.getByTestId('feedback-headword')).toHaveText('keyboard');
    await expect(page.getByTestId('feedback-note')).toHaveCount(0);
  });

  test('a near miss says what was off', async ({ request, page }) => {
    await seedWords(request, ['keyboard']);
    await seedCardFor(request, 'keyboard', ExerciseType.MeaningToWordCuedType);
    await openStudy(page);

    await page.getByTestId('typed-input').fill('keybaord');
    await page.getByTestId('typed-submit').click();

    await expect(page.getByTestId('feedback-correct')).toContainText('Nearly');
    await expect(page.getByTestId('feedback-note')).toContainText('one letter');
  });

  test('a wrong word shows what was typed', async ({ request, page }) => {
    await seedWords(request, ['keyboard']);
    await seedCardFor(request, 'keyboard', ExerciseType.MeaningToWordCuedType);
    await openStudy(page);

    await page.getByTestId('typed-input').fill('mouse');
    await page.getByTestId('typed-submit').click();

    await expect(page.getByTestId('feedback-incorrect')).toBeVisible();
    await expect(page.getByTestId('feedback-chosen')).toContainText('You typed');
    await expect(page.getByTestId('feedback-chosen-text')).toHaveText('mouse');
  });

  test('French gets a row of accented letters that type where the cursor is', async ({ request, page }) => {
    await seedWords(request, frenchWords());
    await seedCardFor(request, 'fenêtre', ExerciseType.MeaningToWordCuedType, {}, { syllables: true });
    await isolateWord(request, 'fenêtre', 'fr');
    await openStudy(page, 'fr');

    await expect(page.getByTestId('accent-bar')).toBeVisible();

    // The noun's article sits in front of the input, so its gender is seen every time.
    await expect(page.getByTestId('noun-article')).toHaveText('la');

    const input = page.getByTestId('typed-input');
    await input.fill('fentre');
    await input.press('Home');
    for (let i = 0; i < 3; i++) {
      await input.press('ArrowRight');
    }
    await page.getByTestId('accent-key').filter({ hasText: 'ê' }).click();

    await expect(input).toHaveValue('fenêtre');

    await input.press('Enter');
    await expect(page.getByTestId('feedback-correct')).toContainText('Correct');
  });

  test('the cued exercise shows the first letters of the word', async ({ request, page }) => {
    await seedWords(request, ['keyboard']);
    await seedCardFor(request, 'keyboard', ExerciseType.MeaningToWordCuedType);
    await openStudy(page);

    await expect(page.getByTestId('letter-mask')).toHaveText('k e y b _ _ _ _');

    await page.getByTestId('typed-input').fill('keyboard');
    await page.getByTestId('typed-input').press('Enter');

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
  });

  test('giving up on typing is recorded as a miss', async ({ request, page }) => {
    await seedWords(request, ['keyboard']);
    await seedCardFor(request, 'keyboard', ExerciseType.MeaningToWordCuedType);
    await openStudy(page);

    await page.getByTestId('typed-give-up').click();

    await expect(page.getByTestId('feedback-incorrect')).toBeVisible();
    expect((await getCard(request, 'keyboard')).gradedReviews).toBe(1);
  });

  test('the syllable scramble is assembled from syllable tiles', async ({ request, page }) => {
    await seedWords(request, ['chocolate']);
    await seedCardFor(request, 'chocolate', ExerciseType.MeaningToWordSyllableScramble, {}, { syllables: true });
    await openStudy(page);

    await expect(page.getByTestId('scramble-exercise')).toBeVisible();
    await expect(page.getByTestId('scramble-tile')).toHaveCount(3);
    await expect(page.getByTestId('scramble-answer')).toContainText('Tap the syllables in order');
    await expect(page.getByTestId('exercise-label')).toHaveText('Put the syllables in order');

    for (const syllable of ['cho', 'co', 'late']) {
      await page.getByTestId('scramble-tile').filter({ hasText: new RegExp(`^${syllable}$`) }).click();
    }

    await page.getByTestId('scramble-submit').click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();
  });

  test('the missing word is picked for its sentence', async ({ request, page }) => {
    await seedWords(request, Array.from({ length: 6 }, (_, i) => `gap${String(i).padStart(2, '0')}`));
    await seedCardFor(request, 'gap00', ExerciseType.ContextToWordChoice);
    await isolateWord(request, 'gap00');
    await openStudy(page);

    await expect(page.getByTestId('choice-exercise')).toBeVisible();
    await expect(page.getByTestId('exercise-prompt')).toContainText('_____');
    await expect(page.getByTestId('exercise-label')).toHaveText('Pick the missing word');
    await expect(page.getByTestId('dictionary-form-note')).toBeVisible();

    // The translation of the missing word, for when the sentence fits more than one option
    await expect(page.getByTestId('hint-text')).toHaveCount(0);
    await page.getByTestId('hint-button').click();
    await expect(page.getByTestId('hint-text')).toHaveText('the meaning of gap00');

    await page.getByTestId('choice-option').filter({ hasText: /^gap00$/ }).click();

    await expect(page.getByTestId('feedback-correct')).toBeVisible();

    // Free: the success counted as a clean one
    const after = await getCard(request, 'gap00');
    expect(after.rungStreak).toBe(2);
  });
});
