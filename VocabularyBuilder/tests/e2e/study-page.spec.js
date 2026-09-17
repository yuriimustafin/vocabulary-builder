const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, seedBareWords, seedCard, getCard, isolateWord
} = require('./helpers/study-helpers');

/**
 * The page itself, driven the way a learner would.
 *
 * The specs above check the rules through the API; these check that the session actually
 * renders them, records what the learner did, and moves on.
 */
test.describe('Study page', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);

    page.on('console', message => {
      if (message.type() === 'error') {
        console.log(`Browser console error: ${message.text()}`);
      }
    });
  });

  /**
   * Waits for the session to have settled into one of its states rather than for the
   * network to go quiet, which the dev server's own traffic can keep from happening.
   */
  async function openStudy(page) {
    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });

    await expect(
      page.getByTestId('study-card')
        .or(page.getByTestId('preparing-words'))
        .or(page.getByTestId('study-done'))
        .or(page.getByTestId('nothing-to-study'))
    ).toBeVisible({ timeout: 30000 });
  }

  test('a new word is shown with its meaning and only asks to be acknowledged',
    async ({ request, page }) => {
      await seedWords(request, ['metword']);
      await openStudy(page);

      await expect(page.getByTestId('introduction-card')).toBeVisible();
      await expect(page.getByTestId('exercise-prompt')).toHaveText('metword');
      await expect(page.getByTestId('introduction-meaning')).toHaveText('the meaning of metword');

      // Nothing to grade: the word has been read, not recalled.
      await expect(page.getByTestId('grade-bar')).toHaveCount(0);
      await expect(page.getByTestId('reveal-button')).toHaveCount(0);

      await page.getByTestId('introduction-acknowledge').click();

      const card = await getCard(request, 'metword');
      expect(card.gradedReviews).toBe(0);
      expect(card.easeFactor).toBe(2.5);
    });

  test('a flashcard reveals its answer and takes a grade', async ({ request, page }) => {
    await seedWords(request, ['pageone']);
    await seedCard(request, {
      headword: 'pageone', rung: 0, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });
    await openStudy(page);

    await expect(page.getByTestId('study-card')).toBeVisible();
    await expect(page.getByTestId('exercise-prompt')).toHaveText('pageone');

    await page.getByTestId('reveal-button').click();

    await expect(page.getByTestId('exercise-answer')).toHaveText('the meaning of pageone');
    await expect(page.getByTestId('grade-bar')).toBeVisible();

    await page.getByTestId('grade-good').click();

    // Only one word was due, so the session reports itself finished.
    await expect(page.getByTestId('study-done')).toBeVisible();

    const card = await getCard(request, 'pageone');
    expect(card.gradedReviews).toBe(1);
  });

  test('the keyboard drives reveal and grading', async ({ request, page }) => {
    await seedWords(request, ['keyword01']);
    await seedCard(request, {
      headword: 'keyword01', rung: 0, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });
    await openStudy(page);

    await page.keyboard.press('Space');
    await expect(page.getByTestId('exercise-answer')).toBeVisible();

    await page.keyboard.press('3');
    await expect(page.getByTestId('study-done')).toBeVisible();

    expect((await getCard(request, 'keyword01')).gradedReviews).toBe(1);
  });

  test('meeting the new words rolls straight on into testing them', async ({ request, page }) => {
    await seedWords(request, ['seqone', 'seqtwo', 'seqthree']);
    await openStudy(page);

    for (let card = 0; card < 3; card++) {
      await expect(page.getByTestId('session-progress')).toContainText(`Card ${card + 1} of 3`);
      await expect(page.getByTestId('introduction-card')).toBeVisible();
      await page.getByTestId('introduction-acknowledge').click();
    }

    // The steps for these words are a minute out. Rather than stopping and asking the
    // learner to come back, the session pulls them forward and keeps going.
    await expect(page.getByTestId('study-card')).toBeVisible();
    await expect(page.getByTestId('introduction-card')).toHaveCount(0);
    await expect(page.getByTestId('study-done')).toHaveCount(0);
  });

  test('the whole first day can be worked through in one sitting', async ({ request, page }) => {
    // Six words is eighteen interactions once each has been met and tested twice, with a
    // refetch between batches, so this needs more than the default budget.
    test.setTimeout(120_000);

    await seedWords(request, Array.from({ length: 6 }, (_, i) => `day${String(i).padStart(2, '0')}`));
    await openStudy(page);

    // Meet and test whatever is put in front of us until the session genuinely runs out.
    // Whichever control is showing gets clicked; the session decides what comes next.
    const controls = [
      'feedback-continue',        // a marked answer holds the word on screen
      'introduction-acknowledge', // a word being met
      'choice-option',            // multiple choice
      'reveal-button',            // a self-graded card, revealed first
      'grade-good',               // then judged
      'scramble-give-up'          // spelling, which this driver does not attempt
    ];

    for (let step = 0; step < 60; step++) {
      if (await page.getByTestId('study-done').count() > 0) {
        break;
      }

      let acted = false;

      for (const control of controls) {
        if (await clickIfPresent(page, control)) {
          acted = true;
          break;
        }
      }

      if (!acted) {
        await page.waitForTimeout(200);
      }
    }

    // Every word met, and none of them still sitting on its first showing.
    const stats = await request.get('/api/en/study/stats').then(r => r.json());
    expect(stats.newToday).toBe(6);
    expect(stats.reviewedToday).toBeGreaterThan(0);
  });

  test('a multiple-choice card is answered by clicking an option', async ({ request, page }) => {
    await seedWords(request, Array.from({ length: 10 }, (_, i) => `mc${String(i).padStart(2, '0')}`));
    await seedCard(request, {
      headword: 'mc00', rung: 1, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    // Keep the rest out of the way so mc00 is the only card in the session. The others
    // still serve as distractors: the pool is drawn from the words, not from their cards.
    await isolateWord(request, 'mc00');

    await openStudy(page);

    await expect(page.getByTestId('choice-exercise')).toBeVisible();
    await expect(page.getByTestId('choice-option')).toHaveCount(4);

    await page.getByTestId('choice-option').filter({ hasText: 'the meaning of mc00' }).click();

    // The answer is marked and the word held on screen, since a multiple-choice question
    // never revealed it.
    await expect(page.getByTestId('feedback-correct')).toBeVisible();
    await page.getByTestId('feedback-continue').click();

    await expect(page.getByTestId('study-done')).toBeVisible();
    expect((await getCard(request, 'mc00')).gradedReviews).toBe(1);
  });

  test('failing a word plays out the diminishing-cues sequence', async ({ request, page }) => {
    await seedWords(request, ['failword']);
    await seedCard(request, {
      headword: 'failword', rung: 0, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });
    await openStudy(page);

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-again').click();

    // First cue: one letter of the word.
    await expect(page.getByTestId('follow-up-note')).toBeVisible();
    await expect(page.getByTestId('letter-mask')).toHaveText('f _ _ _ _ _ _ _');

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-good').click();

    // Second cue: more of it, but never all.
    await expect(page.getByTestId('letter-mask')).toHaveText('f a i _ _ _ _ _');

    await page.getByTestId('reveal-button').click();
    await page.getByTestId('grade-good').click();

    // Then the letters as tiles.
    await expect(page.getByTestId('scramble-exercise')).toBeVisible();
  });

  test('the scramble is assembled from tiles', async ({ request, page }) => {
    await seedWords(request, ['abc']);
    await seedCard(request, {
      headword: 'abc', rung: 4, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await openStudy(page);

    await expect(page.getByTestId('scramble-exercise')).toBeVisible();

    for (const letter of ['a', 'b', 'c']) {
      await page.getByTestId('scramble-tile').filter({ hasText: letter }).first().click();
    }

    await page.getByTestId('scramble-submit').click();

    // Spelling is marked too, so the word is held on screen before moving on.
    await expect(page.getByTestId('feedback-correct')).toBeVisible();
    await page.getByTestId('feedback-continue').click();

    await expect(page.getByTestId('study-done')).toBeVisible();
    expect((await getCard(request, 'abc')).gradedReviews).toBe(1);
  });

  test('a cloze hides its hint until it is asked for', async ({ request, page }) => {
    await seedWords(request, ['hintword']);
    await seedCard(request, {
      headword: 'hintword', rung: 3, state: 2, intervalDays: 3, dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    await openStudy(page);

    await expect(page.getByTestId('exercise-prompt')).toContainText('_____');
    await expect(page.getByTestId('hint-text')).toHaveCount(0);

    await page.getByTestId('hint-button').click();

    await expect(page.getByTestId('hint-text')).toHaveText('the meaning of hintword');
  });

  test('the page waits while words are still being prepared', async ({ request, page }) => {
    await seedBareWords(request, ['zzfailpage']);

    await openStudy(page);

    // The only word cannot be filled in, so the session has nothing to show but says so
    // rather than failing.
    await expect(
      page.getByTestId('preparing-words').or(page.getByTestId('study-done'))
    ).toBeVisible();
  });

  test('an empty collection says so instead of looking broken', async ({ page }) => {
    await openStudy(page);

    await expect(page.getByTestId('nothing-to-study')).toBeVisible();
  });

  test('a new word is badged as new', async ({ request, page }) => {
    await seedWords(request, ['badgeword']);
    await openStudy(page);

    await expect(page.getByTestId('new-badge')).toBeVisible();
  });

  test('the nav offers the study page', async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#root', { timeout: 60000 });

    await page.getByRole('link', { name: 'Study' }).click();

    await expect(page).toHaveURL(/\/study$/);
  });
});

/**
 * Clicks a control if it happens to be on screen.
 *
 * The session re-renders as soon as an answer lands, so checking for an element and then
 * clicking it are two moments with a transition in between. A detached element simply means
 * the card moved on, which is success rather than failure.
 */
async function clickIfPresent(page, testId) {
  const locator = page.getByTestId(testId).first();

  try {
    if (await locator.count() === 0) {
      return false;
    }

    await locator.click({ timeout: 2000 });
    return true;
  } catch {
    return false;
  }
}
