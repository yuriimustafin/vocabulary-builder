const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, getQueue, getStats, submitReview, cardFor, advanceClock, getCard
} = require('./helpers/study-helpers');

/**
 * What a session is made of: everything due, plus what is left of the day's new words.
 *
 * Serial because every test in the suite shares one in-memory database and one clock.
 */
test.describe('Study queue', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  test('introduces new words and renders an exercise for each', async ({ request }) => {
    await seedWords(request, ['alpha', 'bravo', 'charlie']);

    const queue = await getQueue(request);

    expect(queue.cards).toHaveLength(3);
    expect(queue.pendingEnrichmentCount).toBe(0);

    for (const card of queue.cards) {
      expect(card.exercise.prompt).toBeTruthy();
      expect(card.attemptId).toBeTruthy();
      expect(card.isNew).toBe(true);
    }
  });

  test('holds the daily cap however many times the page is refreshed', async ({ request }) => {
    const words = Array.from({ length: 40 }, (_, i) => `cap${String(i).padStart(2, '0')}`);
    await seedWords(request, words);

    let queue;
    for (let refresh = 0; refresh < 5; refresh++) {
      queue = await getQueue(request);
    }

    // The cap is derived from what has been introduced today, not from a counter that a
    // refresh could advance.
    expect(queue.cards.length).toBe(queue.newCardsPerDay);
    expect(queue.newToday).toBe(queue.newCardsPerDay);
  });

  test('lets more through once the day rolls over', async ({ request }) => {
    const words = Array.from({ length: 40 }, (_, i) => `roll${String(i).padStart(2, '0')}`);
    await seedWords(request, words);

    const first = await getQueue(request);
    expect(first.newToday).toBe(first.newCardsPerDay);

    await advanceClock(request, { days: 1 });

    const second = await getQueue(request);
    expect(second.newToday).toBeGreaterThan(0);
    expect(second.newToday).toBeLessThanOrEqual(second.newCardsPerDay);
  });

  test('a marked word does not wait behind the frequency list', async ({ request }) => {
    // Rare enough that frequency alone would never reach it.
    const filler = Array.from({ length: 30 }, (_, i) => ({
      headword: `filler${String(i).padStart(2, '0')}`,
      frequency: 100 + i
    }));

    await seedWords(request, [
      ...filler,
      { headword: 'obscure', frequency: 999999, isMarkedForStudy: true }
    ]);

    const queue = await getQueue(request);

    expect(cardFor(queue, 'obscure')).not.toBeNull();
  });

  test('the mark is spent once the word has been introduced', async ({ request }) => {
    await seedWords(request, [{ headword: 'marked', isMarkedForStudy: true }]);

    await getQueue(request);

    const words = await request.get('/api/en/words?pageSize=100').then(r => r.json());
    const marked = words.items.find(w => w.headword === 'marked');

    expect(marked.isMarkedForStudy ?? false).toBe(false);
  });

  test('words met most often are preferred over merely common ones', async ({ request }) => {
    await seedWords(request, [
      ...Array.from({ length: 20 }, (_, i) => ({
        headword: `common${String(i).padStart(2, '0')}`,
        frequency: 10 + i
      })),
      { headword: 'encountered', frequency: 900000, encounterCount: 9 }
    ]);

    const queue = await getQueue(request);

    expect(cardFor(queue, 'encountered')).not.toBeNull();
  });

  test('reports nothing to do when everything due has been answered', async ({ request }) => {
    await seedWords(request, ['solo']);

    const queue = await getQueue(request);
    await submitReview(request, queue.cards[0], { selfGrade: 3 });

    // The card is now in a learning step a minute away, so nothing is due this instant.
    const after = await getQueue(request);
    expect(after.cards).toHaveLength(0);
    expect(after.pendingEnrichmentCount).toBe(0);
  });

  test('a suspended word stays out of the session', async ({ request }) => {
    await seedWords(request, ['suspendme', 'keepme']);

    const queue = await getQueue(request);
    const target = cardFor(queue, 'suspendme');

    const response = await request.post(`/api/en/study/cards/${target.cardId}/suspend`);
    expect(response.ok()).toBeTruthy();

    await advanceClock(request, { days: 1 });

    const after = await getQueue(request);
    expect(cardFor(after, 'suspendme')).toBeNull();
  });

  test('resuming a suspended word brings it back now', async ({ request }) => {
    await seedWords(request, ['pausedword']);

    const queue = await getQueue(request);
    const target = cardFor(queue, 'pausedword');

    await request.post(`/api/en/study/cards/${target.cardId}/suspend`);
    await request.post(`/api/en/study/cards/${target.cardId}/resume`);

    const after = await getQueue(request);
    expect(cardFor(after, 'pausedword')).not.toBeNull();
  });

  test('stats line up with what was actually reviewed', async ({ request }) => {
    await seedWords(request, ['statone', 'stattwo', 'statthree']);

    const queue = await getQueue(request);
    await submitReview(request, queue.cards[0], { selfGrade: 3 });
    await submitReview(request, queue.cards[1], { selfGrade: 3 });

    const stats = await getStats(request);

    expect(stats.reviewedToday).toBe(2);
    expect(stats.newToday).toBe(3);
    expect(stats.learning).toBe(3);
    expect(stats.notStarted).toBe(0);
  });

  test('only the chosen language is studied', async ({ request }) => {
    await seedWords(request, [
      { headword: 'englishword' },
      { headword: 'motfrancais', language: 1 }
    ]);

    const queue = await getQueue(request, { lang: 'en' });

    expect(cardFor(queue, 'englishword')).not.toBeNull();
    expect(cardFor(queue, 'motfrancais')).toBeNull();
  });

  test('resetting a word puts it back to the beginning', async ({ request }) => {
    await seedWords(request, ['resetme']);
    await seedCardAtRung(request, 'resetme', 4);

    const before = await getCard(request, 'resetme');
    expect(before.rung).toBe(4);

    const response = await request.post(`/api/en/study/cards/${before.id}/reset`);
    expect(response.ok()).toBeTruthy();

    const after = await getCard(request, 'resetme');
    expect(after.rung).toBe(0);
    expect(after.intervalDays).toBe(0);
    expect(after.state).toBe(0);
  });
});

async function seedCardAtRung(request, headword, rung) {
  const { seedCard } = require('./helpers/study-helpers');
  return seedCard(request, { headword, rung, state: 2, intervalDays: 10, dueInDays: -1 });
}
