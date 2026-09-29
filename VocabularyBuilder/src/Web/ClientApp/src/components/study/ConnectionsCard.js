import React from 'react';
import { Button } from 'reactstrap';
import { NounArticle } from '../NounArticle';
import { ReviewGrade } from './exerciseTypes';
import { WordConnections } from './WordConnections';

/**
 * Shown after a miss, before the word is asked again: the word, what it means, and what
 * ties it to things already known - the mnemonic above all, since this is the moment it is
 * there for.
 */
export function ConnectionsCard({ exercise, onGrade, submitting }) {
  return (
    <div data-testid="connections-card">
      <div className="display-6 fw-bold">
        <NounArticle article={exercise.article} />
        <span data-testid="exercise-prompt">{exercise.prompt}</span>
      </div>
      {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
      {exercise.answer && <div className="fs-5 mt-2" data-testid="connections-meaning">{exercise.answer}</div>}

      <WordConnections connections={exercise.connections} />

      <Button color="primary" className="mt-4" disabled={submitting} data-testid="connections-continue"
              onClick={() => onGrade(ReviewGrade.Good)}>
        Got it, ask me again
      </Button>
    </div>
  );
}
