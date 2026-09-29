// Mirrors VocabularyBuilder.Domain.Enums.ExerciseType, which serialises as a number.
export const ExerciseType = {
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
  ContextToWordChoice: 10,
  WordToCollocatesChoice: 11,
  TranslationToSentenceScramble: 12,
  WordToConnectionsReveal: 13
};

// Mirrors VocabularyBuilder.Domain.Enums.GradingMode.
export const GradingMode = {
  SelfReported: 0,
  Automatic: 1
};

// Mirrors VocabularyBuilder.Domain.Enums.ReviewGrade.
export const ReviewGrade = {
  Again: 1,
  Hard: 2,
  Good: 3,
  Easy: 4
};

export const CardDifficulty = {
  Comfortable: 0,
  Shaky: 1,
  Difficult: 2
};

export const EXERCISE_LABELS = {
  [ExerciseType.WordToMeaningReveal]: 'Recall the meaning',
  [ExerciseType.WordToMeaningChoice]: 'Pick the meaning',
  [ExerciseType.MeaningToWordChoice]: 'Pick the word',
  [ExerciseType.ContextToWordRecall]: 'Fill the gap',
  [ExerciseType.MeaningToWordScramble]: 'Spell it out',
  [ExerciseType.MeaningToWordRecall]: 'Recall the word',
  [ExerciseType.MeaningToWordPartialLetters]: 'Recall with a hint',
  [ExerciseType.MeaningToWordSyllableScramble]: 'Put the syllables in order',
  [ExerciseType.MeaningToWordType]: 'Type the word',
  [ExerciseType.MeaningToWordCuedType]: 'Finish the word',
  [ExerciseType.ContextToWordChoice]: 'Pick the missing word',
  [ExerciseType.WordToCollocatesChoice]: 'What goes with it?',
  [ExerciseType.TranslationToSentenceScramble]: 'Build the sentence',
  [ExerciseType.WordToConnectionsReveal]: 'Remember it by'
};

/** Exercises answered by typing, which need the keyboard to themselves. */
export const TYPED_EXERCISES = new Set([ExerciseType.MeaningToWordType, ExerciseType.MeaningToWordCuedType]);
