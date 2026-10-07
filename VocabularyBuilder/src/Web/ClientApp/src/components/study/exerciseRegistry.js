import { ChoiceExercise } from './ChoiceExercise';
import { ScrambleExercise } from './ScrambleExercise';
import { SentenceRebuildExercise } from './SentenceRebuildExercise';
import { SpellingExercise } from './SpellingExercise';
import { ConnectionsCard } from './ConnectionsCard';
import { TypedExercise } from './TypedExercise';
import {
  ContextToWordRecallExercise,
  MeaningToWordPartialLettersExercise,
  MeaningToWordRecallExercise,
  WordToMeaningRevealExercise
} from './SelfGradedExercises';
import { ExerciseType } from './exerciseTypes';

/**
 * Which component renders which exercise.
 *
 * This is the whole client-side cost of a new exercise type: write the component, add one
 * line here. The session shell reads from this map and knows nothing else about the types.
 */
export const EXERCISE_COMPONENTS = {
  [ExerciseType.WordToMeaningReveal]: WordToMeaningRevealExercise,
  [ExerciseType.WordToMeaningChoice]: ChoiceExercise,
  [ExerciseType.MeaningToWordChoice]: ChoiceExercise,
  [ExerciseType.ContextToWordRecall]: ContextToWordRecallExercise,
  [ExerciseType.MeaningToWordScramble]: ScrambleExercise,
  [ExerciseType.MeaningToWordRecall]: MeaningToWordRecallExercise,
  [ExerciseType.MeaningToWordPartialLetters]: MeaningToWordPartialLettersExercise,
  [ExerciseType.MeaningToWordType]: TypedExercise,
  [ExerciseType.MeaningToWordCuedType]: TypedExercise,
  [ExerciseType.ContextToWordChoice]: ChoiceExercise,
  [ExerciseType.TranslationToSentenceScramble]: SentenceRebuildExercise,
  [ExerciseType.WordToConnectionsReveal]: ConnectionsCard,
  [ExerciseType.WordToSpellingCopy]: SpellingExercise,
  [ExerciseType.WordToSpellingCover]: SpellingExercise
};

export function componentFor(exerciseType) {
  return EXERCISE_COMPONENTS[exerciseType] || null;
}
