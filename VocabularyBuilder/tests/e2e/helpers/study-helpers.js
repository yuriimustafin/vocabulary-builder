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
  MeaningToWordPartialLetters: 6,
  MeaningToWordSyllableScramble: 7,
  MeaningToWordType: 8,
  MeaningToWordCuedType: 9,
  ContextToWordChoice: 10
};

/**
 * The shipped ladder (appsettings.json, Study:Ladder): four levels, each a pool of
 * exercises, easiest first. A card's rung is its level; which exercise it is asked there
 * depends on its streak on the level and the exercise it was asked last.
 */
const Ladder = [
  [ExerciseType.WordToMeaningReveal],
  [ExerciseType.MeaningToWordChoice, ExerciseType.ContextToWordChoice, ExerciseType.WordToMeaningChoice],
  [ExerciseType.MeaningToWordSyllableScramble, ExerciseType.MeaningToWordScramble, ExerciseType.MeaningToWordCuedType],
  [ExerciseType.ContextToWordRecall, ExerciseType.MeaningToWordRecall]
];

/** Clean successes that move a word up from each level; zero for the top, which it never leaves. */
const PromoteAfter = [1, 2, 3, 0];

/** The level a given exercise sits on. */
function rungOf(type) {
  const rung = Ladder.findIndex(level => level.includes(type));

  if (rung < 0) {
    throw new Error(`Exercise type ${type} is not on the ladder`);
  }

  return rung;
}

/**
 * The streak that points a word at this exercise within its level.
 *
 * The syllable scramble is only offered for words of three syllables or more, which the
 * short made-up words most specs use never are - so for those it is not in the pool, and
 * the exercises after it sit one place earlier. Pass syllables: true for a word that has them.
 */
function streakFor(type, { syllables = false } = {}) {
  const pool = Ladder[rungOf(type)].filter(t =>
    syllables || t === type || t !== ExerciseType.MeaningToWordSyllableScramble);

  return pool.indexOf(type);
}

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

/**
 * Seeds a card whose next graded exercise will be the one given: a review due now, on that
 * exercise's level with the streak that selects it. Anything in `card` overrides the rest.
 */
function seedCardFor(request, headword, type, card = {}, { syllables = false } = {}) {
  return seedCard(request, {
    headword,
    rung: rungOf(type),
    rungStreak: streakFor(type, { syllables }),
    state: CardState.Review,
    intervalDays: 3,
    dueInDays: -0.1,
    lastReviewedDaysAgo: 1,
    ...card
  });
}

/**
 * The right answer to whatever a card is asking: the option that is the word or its
 * meaning, the word itself for anything built or typed, or a grade for a self-graded card.
 */
function correctAnswer(card, grade = ReviewGrade.Good) {
  const { exercise } = card;

  if (exercise.gradingMode === 0) {
    return { selfGrade: grade };
  }

  if (exercise.type === ExerciseType.WordToMeaningChoice) {
    return { answer: exercise.options.find(o => o.includes(card.headword)) };
  }

  return { answer: card.headword };
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
    // As the page does: the sentence the exercise was asked on goes back with it
    exampleId: card.exercise.exampleId,
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

/**
 * Waits for a word's study content - its examples and connections - to be generated by the
 * background worker, and returns the word's details. The queue is fetched on each try,
 * since rendering the word is what asks for its content.
 */
async function waitForStudyContent(request, headword, { lang = 'en', attempts = 25, intervalMs = 400 } = {}) {
  for (let attempt = 0; attempt < attempts; attempt++) {
    await getQueue(request, { lang });
    const card = await getCard(request, headword);

    if (card) {
      const details = await request.get(`/api/${lang}/words/${card.wordId}/details`).then(r => r.json());

      if (details.connections && details.studyExamples.length > 0) {
        return details;
      }
    }

    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }

  throw new Error(`No study content was generated for ${headword}`);
}

/** Finds a queued card by word, whatever position it is in. */
function cardFor(queue, headword) {
  return queue.cards.find(card => card.headword === headword) || null;
}

module.exports = {
  STUDY_API,
  ExerciseType,
  Ladder,
  PromoteAfter,
  rungOf,
  streakFor,
  seedCardFor,
  correctAnswer,
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
  waitForStudyContent,
  cardFor
};
