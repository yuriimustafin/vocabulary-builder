const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const {
  seedWords, seedBareWords, getQueue, getStats, waitForContent, cardFor
} = require('./helpers/study-helpers');

/**
 * Filling in words that arrived without a definition or a usable example.
 *
 * Everything here runs against the mock model - no real call is ever made. A headword
 * beginning "zzfail" makes the mock return nothing, which is how the failure path is
 * reached without depending on a real model misbehaving.
 */
test.describe('Study content enrichment', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
  });

  test('a word with dictionary data is studiable straight away', async ({ request }) => {
    await seedWords(request, ['ready01', 'ready02']);

    const queue = await getQueue(request);

    expect(queue.cards).toHaveLength(2);
    expect(queue.pendingEnrichmentCount).toBe(0);
  });

  test('a bare word waits, then becomes studiable', async ({ request }) => {
    await seedBareWords(request, ['bare01', 'bare02']);

    const first = await getQueue(request);
    expect(first.cards).toHaveLength(0);
    expect(first.pendingEnrichmentCount).toBe(2);

    const filled = await waitForContent(request, 2);

    expect(filled.cards).toHaveLength(2);
    expect(filled.pendingEnrichmentCount).toBe(0);
    expect(filled.cards[0].exercise.answer).toBeTruthy();
  });

  test('a word that cannot be shown yet leaves no card behind', async ({ request }) => {
    await seedBareWords(request, ['trace01']);

    await getQueue(request);

    // The day's allowance must not be spent on a word that was never actually shown.
    const stats = await getStats(request);
    expect(stats.newToday).toBe(0);
  });

  test('ready words are studied while the rest are still being filled in', async ({ request }) => {
    await seedWords(request, ['mixed01', 'mixed02']);
    await seedBareWords(request, ['mixed03', 'mixed04']);

    const queue = await getQueue(request);

    expect(queue.cards.length).toBeGreaterThanOrEqual(2);
    expect(cardFor(queue, 'mixed01')).not.toBeNull();
  });

  test('a word the model cannot handle is skipped rather than breaking the session',
    async ({ request }) => {
      await seedWords(request, ['workingword']);
      await seedBareWords(request, ['zzfailword']);

      const queue = await waitForContent(request, 1);

      // The good word is studiable and the failing one simply is not there.
      expect(cardFor(queue, 'workingword')).not.toBeNull();
      expect(cardFor(queue, 'zzfailword')).toBeNull();
    });

  test('a failing word stops being retried forever', async ({ request }) => {
    await seedBareWords(request, ['zzfailagain']);

    // Several passes is more than the allowed attempts.
    for (let attempt = 0; attempt < 6; attempt++) {
      await getQueue(request);
      await new Promise(resolve => setTimeout(resolve, 300));
    }

    const stats = await getStats(request);

    // It is no longer counted as waiting: the app has given up on it, not hung on it.
    expect(stats.awaitingContent).toBe(0);
  });

  test('a filled-in word is not generated again on the next session', async ({ request }) => {
    await seedBareWords(request, ['once01']);

    await waitForContent(request, 1);
    const first = await getQueue(request);
    const answer = first.cards[0].exercise.answer;

    const second = await getQueue(request);

    expect(second.cards[0].exercise.answer).toBe(answer);
    expect(second.pendingEnrichmentCount).toBe(0);
  });

  test('a generated sentence really can be blanked for a cloze', async ({ request }) => {
    await seedBareWords(request, ['clozeable']);
    await waitForContent(request, 1);

    const { seedCard } = require('./helpers/study-helpers');
    await seedCard(request, {
      headword: 'clozeable', rung: 3, state: 2, intervalDays: 2,
      dueInDays: -0.1, lastReviewedDaysAgo: 1
    });

    const card = cardFor(await getQueue(request), 'clozeable');

    expect(card.exercise.type).toBe(3);
    expect(card.exercise.prompt).toContain('_____');
    expect(card.exercise.prompt).not.toContain('clozeable');
  });
});
