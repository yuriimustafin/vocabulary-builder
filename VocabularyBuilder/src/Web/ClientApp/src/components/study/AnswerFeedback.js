import React from 'react';
import { Alert, Button } from 'reactstrap';
import { NounArticle } from '../NounArticle';
import { BilingualText } from './BilingualText';
import { WordConnections } from './WordConnections';

/**
 * What happened after an automatically graded answer.
 *
 * The word is shown in full either way: seeing it again is worth as much after getting it
 * right as after missing it. When it was missed, the option that was picked is named too -
 * it is another real word from the collection, so saying which one turns a bare "wrong"
 * into a second word met in passing.
 */
/**
 * Names what the chosen option actually was.
 *
 * When the option was a meaning, the useful thing is whose meaning it is. When it was a
 * word - picked, or typed as another word in the collection - the useful thing is what that
 * word means instead. A typed word is always a word, whatever case or article it was typed in.
 */
function describeChoice(chosen, typed) {
  const choseAWord = chosen.headword
    && (typed || chosen.headword.toLowerCase() === chosen.text.trim().toLowerCase());

  if (choseAWord) {
    return chosen.meaning ? `which means: ${chosen.meaning}` : null;
  }

  return chosen.headword ? `That is the meaning of ${chosen.headword}` : null;
}

export function AnswerFeedback({ feedback, onContinue, continuing, language, typed = false }) {
  const { correct, chosen, note } = feedback;

  return (
    <div data-testid="answer-feedback">
      <Alert
        color={correct ? (note ? 'warning' : 'success') : 'danger'}
        className="d-flex align-items-center gap-2 flex-wrap"
        data-testid={correct ? 'feedback-correct' : 'feedback-incorrect'}
      >
        <strong>{correct ? (note ? 'Nearly' : 'Correct') : 'Not quite'}</strong>
        {/* What was off, or - after a miss that costs nothing - that it costs nothing */}
        {note && <span data-testid="feedback-note">{note}</span>}
      </Alert>

      <div className="fs-3 fw-bold">
        <NounArticle article={feedback.article} />
        <span data-testid="feedback-headword">{feedback.headword}</span>
      </div>

      {feedback.transcription && <div className="text-muted">/{feedback.transcription}/</div>}
      {feedback.partOfSpeech && (
        <div className="text-muted small fst-italic">{feedback.partOfSpeech}</div>
      )}

      <BilingualText
        className="fs-5 mt-2"
        language={language}
        learned={feedback.meaningGloss}
        native={feedback.meaning}
        nativeTestId="feedback-meaning"
      />

      <BilingualText
        className="text-muted fst-italic mt-2"
        language={language}
        learned={feedback.contextSentence}
        native={feedback.contextSentenceTranslation}
        learnedTestId="feedback-context"
      />

      {/* The mnemonic is for a word that did not come back on its own */}
      <WordConnections connections={feedback.connections} showMnemonic={!correct} />

      {chosen && (
        <div className="border-start border-3 border-danger ps-3 mt-4" data-testid="feedback-chosen">
          <div className="text-muted small text-uppercase">{typed ? 'You typed' : 'You chose'}</div>
          <div data-testid="feedback-chosen-text">{chosen.text}</div>

          {/*
            Which way round this reads depends on the question. Picking a meaning names a
            word; picking a word names its meaning. Either way the point is the same - the
            wrong option belongs to something, and saying what makes it worth having seen.
          */}
          {describeChoice(chosen, typed) && (
            <div className="text-muted small mt-1" data-testid="feedback-chosen-owner">
              {describeChoice(chosen, typed)}
            </div>
          )}
        </div>
      )}

      <Button
        color="primary"
        className="mt-4"
        disabled={continuing}
        data-testid="feedback-continue"
        onClick={onContinue}
      >
        Continue <span className="opacity-75 small">(space)</span>
      </Button>
    </div>
  );
}
