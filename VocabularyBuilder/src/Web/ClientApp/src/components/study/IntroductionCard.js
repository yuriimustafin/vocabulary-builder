import React from 'react';
import { Button } from 'reactstrap';

/**
 * A word being met for the first time.
 *
 * Nothing is hidden and nothing is graded. The learner has not been tested yet, so asking
 * how well they recalled it would have no honest answer - the word simply goes on the
 * first learning step, and the real test comes a minute later.
 */
export function IntroductionCard({ exercise, onAcknowledge, submitting }) {
  return (
    <div data-testid="introduction-card">
      <div className="display-6 fw-bold" data-testid="exercise-prompt">{exercise.prompt}</div>

      {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
      {exercise.partOfSpeech && <div className="text-muted small fst-italic">{exercise.partOfSpeech}</div>}

      <div className="fs-5 mt-3" data-testid="introduction-meaning">{exercise.answer}</div>

      {exercise.contextSentence && (
        <p className="text-muted fst-italic mt-2" data-testid="introduction-context">
          {exercise.contextSentence}
        </p>
      )}

      <Button
        color="primary"
        className="mt-4"
        disabled={submitting}
        data-testid="introduction-acknowledge"
        onClick={onAcknowledge}
      >
        Got it <span className="opacity-75 small">(space)</span>
      </Button>

      <p className="text-muted small mt-3 mb-0">
        You will be asked about this one shortly.
      </p>
    </div>
  );
}
