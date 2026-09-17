// Mirrors VocabularyBuilder.Domain.Enums.ExerciseType, which serialises as a number.
export const ExerciseType = {
  WordToMeaningReveal: 0,
  WordToMeaningChoice: 1,
  MeaningToWordChoice: 2,
  ContextToWordRecall: 3,
  MeaningToWordScramble: 4,
  MeaningToWordRecall: 5,
  MeaningToWordPartialLetters: 6
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
  [ExerciseType.MeaningToWordPartialLetters]: 'Recall with a hint'
};
