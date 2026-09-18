import React from 'react';
import { Button } from 'reactstrap';
import { NounArticle } from '../NounArticle';
import { BilingualText } from './BilingualText';

/**
 * A word being met for the first time.
 *
 * Nothing is hidden and nothing is graded. The learner has not been tested yet, so asking
 * how well they recalled it would have no honest answer - the word simply goes on the
 * first learning step, and the real test comes a minute later.
 */
export function IntroductionCard({ exercise, onAcknowledge, onAlreadyKnown, submitting, language }) {
  return (
    <div data-testid="introduction-card">
      <div className="display-6 fw-bold">
        <NounArticle article={exercise.article} />
        <span data-testid="exercise-prompt">{exercise.prompt}</span>
      </div>

      {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
      {exercise.partOfSpeech && <div className="text-muted small fst-italic">{exercise.partOfSpeech}</div>}

      <BilingualText
        className="fs-5 mt-3"
        language={language}
        learned={exercise.meaningGloss}
        native={exercise.answer}
        nativeTestId="introduction-meaning"
      />

      <BilingualText
        className="text-muted fst-italic mt-2"
        language={language}
        learned={exercise.contextSentence}
        native={exercise.contextSentenceTranslation}
        learnedTestId="introduction-context"
      />

      <div className="d-flex gap-2 flex-wrap mt-4">
        <Button
          color="primary"
          disabled={submitting}
          data-testid="introduction-acknowledge"
          onClick={onAcknowledge}
        >
          Got it <span className="opacity-75 small">(space)</span>
        </Button>

        {/*
          Recognising a word on sight is worth saying: studying it would spend a place in
          the day on something already learned, and another word takes its place instead.
        */}
        <Button
          color="secondary"
          outline
          disabled={submitting}
          data-testid="introduction-known"
          onClick={onAlreadyKnown}
        >
          I already know this word
        </Button>
      </div>

      <p className="text-muted small mt-3 mb-0">
        You will be asked about this one shortly.
      </p>
    </div>
  );
}
