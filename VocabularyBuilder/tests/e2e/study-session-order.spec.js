const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, getQueue, advanceClock, acknowledgeIntroduction, submitReview
} = require('./helpers/study-helpers');

/**
 * The shape of a first day.
 *
 * Meeting every new word and only then being tested on any of them is not a session, it is
 * a reading list followed by an exam. The tests for the first handful should come back
 * among the words still being introduced.
 *
 * This walks a whole day and looks at the order the cards actually arrived in, because the
 * fault it guards against is invisible to any single request: each batch was correct on its
 * own terms, and the ordering only went wrong across them.
 */
test.describe('First-day session order', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  /**
   * Works through a session the way the page does, answering everything correctly, and
   * records whether each card was a word being met or a word being tested.
   *
   * Time is moved on between cards: a step falling due is what the interleaving depends on,
   * and without a plausible pace nothing would ever come back.
   */
  async function walkSession(request, { secondsPerCard = 12, maxCards = 80 } = {}) {
    const order = [];

    while (order.length < maxCards) {
      const queue = await getQueue(request);

      if (queue.cards.length === 0) {
        break;
      }

      for (const card of queue.cards) {
        order.push(card.isIntroduction ? 'meet' : 'test');

        if (card.isIntroduction) {
          await acknowledgeIntroduction(request, card);
        } else {
          const { exercise } = card;
          const body = exercise.gradingMode === 0
            ? { selfGrade: 3 }
            : {
              answer: exercise.options
                ? exercise.options.find(o => o.includes(card.headword)) || card.headword
                : card.headword
            };

          await submitReview(request, card, body);
        }

        await advanceClock(request, { minutes: secondsPerCard / 60 });
      }
    }

    return order;
  }

  test('tests come back among the words still being introduced', async ({ request }) => {
    test.setTimeout(120_000);

    await seedWords(request, Array.from({ length: 30 }, (_, i) => `so${String(i).padStart(2, '0')}`));

    const order = await walkSession(request);

    const firstTest = order.indexOf('test');
    expect(firstTest).toBeGreaterThan(-1);

    // A few words are met before anything can be tested - at the very start there is
    // nothing to ask about - but it should be a handful, not the whole day's allowance.
    expect(firstTest).toBeLessThanOrEqual(6);

    // And from there the two genuinely mix, rather than the day being met-everything
    // followed by test-everything.
    const lastMeet = order.lastIndexOf('meet');
    const testsBeforeTheLastMeet = order.slice(0, lastMeet).filter(kind => kind === 'test').length;

    expect(testsBeforeTheLastMeet).toBeGreaterThanOrEqual(4);
  });

  test('every new word is met and tested without the session stalling', async ({ request }) => {
    test.setTimeout(120_000);

    await seedWords(request, Array.from({ length: 30 }, (_, i) => `sc${String(i).padStart(2, '0')}`));

    const order = await walkSession(request);

    const met = order.filter(kind => kind === 'meet').length;
    const tested = order.filter(kind => kind === 'test').length;

    expect(met).toBe(12, "the day's allowance is met in full");

    // Two learning steps each, so every word is tested twice before it graduates.
    expect(tested).toBe(met * 2);
  });

  test('a word is not asked about before it has been met', async ({ request }) => {
    test.setTimeout(120_000);

    await seedWords(request, Array.from({ length: 20 }, (_, i) => `sm${String(i).padStart(2, '0')}`));

    const met = new Set();
    let violations = 0;

    for (let batch = 0; batch < 40; batch++) {
      const queue = await getQueue(request);

      if (queue.cards.length === 0) {
        break;
      }

      for (const card of queue.cards) {
        if (card.isIntroduction) {
          met.add(card.headword);
          await acknowledgeIntroduction(request, card);
        } else {
          if (!met.has(card.headword)) {
            violations++;
          }

          await submitReview(request, card, { selfGrade: 3, answer: card.headword });
        }

        await advanceClock(request, { minutes: 0.2 });
      }
    }

    expect(violations).toBe(0);
  });
});
