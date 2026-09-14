const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');
const { seedWords, getQueue, submitReview, cardFor, getCard } = require('./helpers/study-helpers');

/**
 * Answering twice must not score twice.
 *
 * A double click, a retry after a timeout, or a page that resends on reconnect all arrive
 * as a second submit of the same rendered exercise. The attempt id is what makes that
 * harmless.
 */
test.describe('Review idempotency', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request }) => {
    await setupCleanDatabase(request);
    await seedWords(request, Array.from({ length: 6 }, (_, i) => `id${String(i).padStart(2, '0')}`));
  });

  test('a repeated submit reports itself as a duplicate and changes nothing', async ({ request }) => {
    const card = cardFor(await getQueue(request), 'id00');

    const first = await submitReview(request, card, { selfGrade: 3 });
    const after = await getCard(request, 'id00');

    const second = await submitReview(request, card, { selfGrade: 3 });
    const later = await getCard(request, 'id00');

    expect(first.wasDuplicate).toBe(false);
    expect(second.wasDuplicate).toBe(true);

    expect(later.intervalDays).toBe(after.intervalDays);
    expect(later.easeFactor).toBe(after.easeFactor);
    expect(later.rung).toBe(after.rung);
    expect(later.reviewNumber).toBe(after.reviewNumber);
    expect(later.dueAtUtc).toBe(after.dueAtUtc);
  });

  test('only one review is recorded', async ({ request }) => {
    const card = cardFor(await getQueue(request), 'id01');

    await submitReview(request, card, { selfGrade: 3 });
    await submitReview(request, card, { selfGrade: 3 });
    await submitReview(request, card, { selfGrade: 3 });

    expect((await getCard(request, 'id01')).gradedReviews).toBe(1);
  });

  test('a duplicate cannot be used to change the grade', async ({ request }) => {
    const card = cardFor(await getQueue(request), 'id02');

    const first = await submitReview(request, card, { selfGrade: 1 });
    const second = await submitReview(request, card, { selfGrade: 4 });

    // The recorded grade stands; a resend does not get to revise it.
    expect(second.grade).toBe(first.grade);
    expect(second.wasDuplicate).toBe(true);
  });

  test('simultaneous submits of the same attempt still score once', async ({ request }) => {
    const card = cardFor(await getQueue(request), 'id03');

    const results = await Promise.all([
      submitReview(request, card, { selfGrade: 3 }),
      submitReview(request, card, { selfGrade: 3 })
    ]);

    expect(results.filter(r => !r.wasDuplicate)).toHaveLength(1);
    expect((await getCard(request, 'id03')).gradedReviews).toBe(1);
  });

  test('a different attempt on the same card is a genuine second review', async ({ request }) => {
    const first = cardFor(await getQueue(request), 'id04');
    await submitReview(request, first, { selfGrade: 3 });

    const { advanceToDue } = require('./helpers/study-helpers');
    await advanceToDue(request, 'id04');

    const second = cardFor(await getQueue(request), 'id04');
    expect(second.attemptId).not.toBe(first.attemptId);

    const result = await submitReview(request, second, { selfGrade: 3 });

    expect(result.wasDuplicate).toBe(false);
    expect((await getCard(request, 'id04')).gradedReviews).toBe(2);
  });

  test('a repeated follow-up is recorded once', async ({ request }) => {
    const card = cardFor(await getQueue(request), 'id05');
    await submitReview(request, card, { selfGrade: 1 });

    const stored = await getCard(request, 'id05');
    const attemptId = crypto.randomUUID();

    const body = {
      cardId: stored.id,
      attemptId,
      exerciseType: 6,
      elapsedMs: 2000,
      correct: true
    };

    await request.post('/api/en/study/follow-ups', { data: body });
    await request.post('/api/en/study/follow-ups', { data: body });

    expect((await getCard(request, 'id05')).followUps).toBe(1);
  });
});
