import React from 'react';
import { RevealExercise } from './RevealExercise';

/** Flashcard: the word, then its meaning. */
export function WordToMeaningRevealExercise(props) {
  return (
    <RevealExercise
      {...props}
      prompt={props.exercise.prompt}
      promptClassName="display-6 fw-bold"
      support={props.exercise.transcription && (
        <div className="text-muted">/{props.exercise.transcription}/</div>
      )}
    />
  );
}

/**
 * Cloze: the sentence with the word cut out of it. The meaning sits behind a hint the
 * learner can choose to open - except on a probe that was raised after a long absence,
 * where the server sends no hint at all so the grade is not inflated by a cue.
 */
export function ContextToWordRecallExercise(props) {
  return (
    <RevealExercise
      {...props}
      prompt={props.exercise.prompt}
      promptClassName="fs-4 lh-base"
      support={<p className="text-muted small mt-2">Which word fills the gap?</p>}
    />
  );
}

/** Free production: the meaning alone, with the word to be produced unaided. */
export function MeaningToWordRecallExercise(props) {
  return (
    <RevealExercise
      {...props}
      prompt={props.exercise.prompt}
      promptClassName="fs-4 lh-base"
      support={<p className="text-muted small mt-2">Which word means this?</p>}
    />
  );
}

/**
 * A step in the sequence that runs after a word is missed: the meaning plus part of the
 * spelling, with more revealed each time until it can be produced.
 */
export function MeaningToWordPartialLettersExercise(props) {
  return (
    <RevealExercise
      {...props}
      prompt={props.exercise.prompt}
      promptClassName="fs-5 lh-base"
      support={(
        <div className="mt-3">
          <code className="fs-3 letter-mask" data-testid="letter-mask">{props.exercise.letterMask}</code>
        </div>
      )}
    />
  );
}
