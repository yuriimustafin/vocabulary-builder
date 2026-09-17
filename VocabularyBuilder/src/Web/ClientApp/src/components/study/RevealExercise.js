import React, { Component } from 'react';
import { Button } from 'reactstrap';
import { GradeBar } from './GradeBar';

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
      revealed, onReveal, onGrade, submitting
    } = this.props;
    const { hintShown } = this.state;

    return (
      <div data-testid="reveal-exercise">
        <div className={promptClassName} data-testid="exercise-prompt">{prompt}</div>

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
            <div className="fs-4 fw-semibold text-success" data-testid="exercise-answer">{exercise.answer}</div>
            {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
            {exercise.contextSentence && (
              <p className="text-muted fst-italic mt-2" data-testid="answer-context">{exercise.contextSentence}</p>
            )}
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
