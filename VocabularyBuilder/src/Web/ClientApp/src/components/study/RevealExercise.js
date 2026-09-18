import React, { Component } from 'react';
import { Button } from 'reactstrap';
import { GradeBar } from './GradeBar';
import { NounArticle } from '../NounArticle';
import { BilingualText } from './BilingualText';

/**
 * Every self-graded exercise has the same shape: a prompt, an answer the learner asks to
 * see once they have tried to recall it, and then their own judgement of how it went.
 * What differs is only what the prompt is and how much support comes with it.
 *
 * Whether the answer is showing is owned by the session rather than held here, so the
 * keyboard shortcuts can drive it without reaching into this component.
 */
export class RevealExercise extends Component {
  constructor(props) {
    super(props);
    this.state = { hintShown: false };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState({ hintShown: false });
    }
  }

  showHint = () => {
    this.setState({ hintShown: true });
    if (this.props.onHintUsed) {
      this.props.onHintUsed();
    }
  };

  render() {
    const {
      exercise, prompt, promptClassName, support,
      revealed, onReveal, onGrade, submitting,
      promptIsWord, answerIsWord, language
    } = this.props;
    const { hintShown } = this.state;

    return (
      <div data-testid="reveal-exercise">
        <div className={promptClassName}>
          {promptIsWord && <NounArticle article={exercise.article} />}
          <span data-testid="exercise-prompt">{prompt}</span>
        </div>

        {support}

        {exercise.hint && !revealed && (
          hintShown
            ? <p className="text-muted fst-italic mt-3" data-testid="hint-text">{exercise.hint}</p>
            : <Button color="link" className="ps-0 mt-2" data-testid="hint-button" onClick={this.showHint}>
                Show a hint
              </Button>
        )}

        {revealed ? (
          <div className="mt-4">
            <div className="fs-4 fw-semibold text-success">
              {answerIsWord && <NounArticle article={exercise.article} />}
              <span data-testid="exercise-answer">{exercise.answer}</span>
            </div>
            {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
            {/* The gloss names which sense the answer is; only shown once the answer is */}
            {!answerIsWord && exercise.meaningGloss && (
              <BilingualText
                className="text-muted mt-1"
                language={language}
                learned={exercise.meaningGloss}
              />
            )}
            <BilingualText
              className="text-muted fst-italic mt-2"
              language={language}
              learned={exercise.contextSentence}
              native={exercise.contextSentenceTranslation}
              learnedTestId="answer-context"
            />
            <p className="text-muted small mt-3 mb-2">How well did you recall it?</p>
            <GradeBar onGrade={onGrade} disabled={submitting} />
          </div>
        ) : (
          <Button color="primary" className="mt-4" data-testid="reveal-button" onClick={onReveal}>
            Show answer <span className="opacity-75 small">(space)</span>
          </Button>
        )}
      </div>
    );
  }
}
