/**
 * Helpers for the study specs.
 *
 * Words are seeded with a definition and an example wherever the test is not about
 * generation, so the model is never involved and the assertions stay about the ladder.
 * Where generation is the point, the definition is left off and the mock client fills it.
 */

const STUDY_API = '/api/e2e-testing/study';

/** Mirrors VocabularyBuilder.Domain.Enums.ExerciseType. */
const ExerciseType = {
  WordToMeaningReveal: 0,
  WordToMeaningChoice: 1,
  MeaningToWordChoice: 2,
  ContextToWordRecall: 3,
  MeaningToWordScramble: 4,
  MeaningToWordRecall: 5,
  MeaningToWordPartialLetters: 6
};

/** Mirrors VocabularyBuilder.Domain.Enums.CardState. */
const CardState = { New: 0, Learning: 1, Review: 2, Relearning: 3, Suspended: 4 };

const ReviewGrade = { Again: 1, Hard: 2, Good: 3, Easy: 4 };

async function post(request, path, body) {
  const response = await request.post(path, { data: body === undefined ? {} : body });

  if (!response.ok()) {
    throw new Error(`POST ${path} failed: ${response.status()} ${await response.text()}`);
  }

  return response.status() === 204 ? null : response.json();
}

/**
 * Seeds words. A word is given a definition and an example by default, so it is
 * immediately studiable without the mock model being involved.
 */
function seedWords(request, words) {
  const prepared = words.map(word => {
    const headword = typeof word === 'string' ? word : word.headword;
    const base = {
      headword,
      partOfSpeech: 'adjective',
      frequency: 1000,
      definition: `the meaning of ${headword}`,
      example: `A line using ${headword} exactly once.`
    };

    return typeof word === 'string' ? base : { ...base, ...word };
  });

  return post(request, `${STUDY_API}/seed-words`, prepared);
}

/** Seeds words with no content at all, so they have to be generated before they can be studied. */
function seedBareWords(request, headwords) {
  return seedWords(request, headwords.map(headword => ({
    headword,
    definition: null,
    example: null
  })));
}

/** Puts a word's card into an exact state rather than grinding it there through the UI. */
function seedCard(request, card) {
  return post(request, `${STUDY_API}/seed-card`, card);
}

function advanceClock(request, { days = 0, minutes = 0 } = {}) {
  return post(request, `${STUDY_API}/advance-clock`, { days, minutes });
}

/**
 * Moves the clock to just past when a word is next due.
 *
 * Intervals run from minutes to days as a card matures, so a fixed step either waits far
 * longer than needed or silently stops being enough. Overshooting matters too: arriving
 * long after a card was due is itself a signal the scheduler reacts to.
 */
async function advanceToDue(request, headword, { graceMinutes = 1 } = {}) {
  const card = await getCard(request, headword);

  if (!card || !card.dueAtUtc) {
    return null;
  }

  const now = await post(request, `${STUDY_API}/advance-clock`, { days: 0, minutes: 0 });
  const millisecondsUntilDue = parseUtc(card.dueAtUtc) - parseUtc(now.nowUtc);

  return advanceClock(request, {
    minutes: Math.max(0, millisecondsUntilDue / 60000) + graceMinutes
  });
}

/**
 * Both values are UTC, but only some of them say so: a column read back from SQLite has no
 * kind and serialises without the trailing Z, while a value produced in memory keeps it.
 */
function parseUtc(value) {
  return Date.parse(value.endsWith('Z') ? value : `${value}Z`);
}

async function getCard(request, headword) {
  const response = await request.get(`${STUDY_API}/card/${headword}`);
  return response.ok() ? response.json() : null;
}

async function getQueue(request, { lang = 'en', limit = 60 } = {}) {
  const response = await request.get(`/api/${lang}/study/queue?limit=${limit}`);

  if (!response.ok()) {
    throw new Error(`Queue failed: ${response.status()} ${await response.text()}`);
  }

  return response.json();
}

function getStats(request, lang = 'en') {
  return request.get(`/api/${lang}/study/stats`).then(r => r.json());
}

/** Answers one card through the API, the way the page does. */
function submitReview(request, card, body, lang = 'en') {
  return post(request, `/api/${lang}/study/reviews`, {
    cardId: card.cardId,
    attemptId: card.attemptId,
    exerciseType: card.exercise.type,
    elapsedMs: 4000,
    ...body
  });
}

/**
 * Records that a word has been met. The first showing is not graded - the learner has read
 * the word, not recalled it - so it goes to its own endpoint.
 */
function acknowledgeIntroduction(request, card, lang = 'en') {
  return post(request, `/api/${lang}/study/introductions`, {
    cardId: card.cardId,
    attemptId: card.attemptId,
    exerciseType: card.exercise.type,
    elapsedMs: 3000
  });
}

/** Answers a card whichever way it is asking, so a caller need not care which it got. */
function answerCard(request, card, body, lang = 'en') {
  return card.isIntroduction
    ? acknowledgeIntroduction(request, card, lang)
    : submitReview(request, card, body, lang);
}

/**
 * Works through batches the way a session does, until every new word for the day has been
 * met. Batches are small on purpose, so one call is not the whole day's allowance.
 */
async function introduceAllNewWords(request, { maxBatches = 20 } = {}) {
  const met = [];

  for (let batch = 0; batch < maxBatches; batch++) {
    const queue = await getQueue(request);
    const introductions = queue.cards.filter(card => card.isIntroduction);

    if (introductions.length === 0) {
      return met;
    }

    for (const card of introductions) {
      await acknowledgeIntroduction(request, card);
      met.push(card.headword);
    }
  }

  return met;
}

/**
 * Leaves one word as the only thing the session will offer.
 *
 * Batches are small, so a single queue call introduces only a few of the words on hand.
 * Everything has to be brought into play before the rest can be put aside, or the next
 * batch quietly introduces more.
 */
async function isolateWord(request, headword, lang = 'en') {
  for (let batch = 0; batch < 20; batch++) {
    const queue = await getQueue(request, { lang });
    const others = queue.cards.filter(card => card.headword !== headword);

    if (others.length === 0 && queue.cards.length > 0) {
      return;
    }

    for (const card of others) {
      await request.post(`/api/${lang}/study/cards/${card.cardId}/suspend`);
    }

    if (queue.cards.length === 0) {
      return;
    }
  }
}

/**
 * Waits until the background worker has filled a word in. Enrichment is deliberately not
 * pushed to the client, so polling is what the page itself does.
 */
async function waitForContent(request, expectedCards, { attempts = 20, intervalMs = 400 } = {}) {
  for (let attempt = 0; attempt < attempts; attempt++) {
    const queue = await getQueue(request);

    if (queue.cards.length >= expectedCards) {
      return queue;
    }

    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }

  return getQueue(request);
}

/** Finds a queued card by word, whatever position it is in. */
function cardFor(queue, headword) {
  return queue.cards.find(card => card.headword === headword) || null;
}

module.exports = {
  STUDY_API,
  ExerciseType,
  CardState,
  ReviewGrade,
  seedWords,
  seedBareWords,
  seedCard,
  advanceClock,
  advanceToDue,
  getCard,
  getQueue,
  getStats,
  submitReview,
  acknowledgeIntroduction,
  answerCard,
  introduceAllNewWords,
  isolateWord,
  waitForContent,
  cardFor
};
