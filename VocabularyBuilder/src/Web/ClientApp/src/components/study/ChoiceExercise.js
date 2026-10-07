import React, { Component } from 'react';
import { Button } from 'reactstrap';
import { NounArticle } from '../NounArticle';
import { ExerciseType } from './exerciseTypes';

/**
 * Multiple choice, in either direction.
 *
 * The payload carries no indication of which option is right - the server marks the
 * answer against the word when it comes back - so there is nothing here to give away.
 */
export class ChoiceExercise extends Component {
  constructor(props) {
    super(props);
    this.state = { chosen: null, hintShown: false };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState({ chosen: null, hintShown: false });
    }
  }

  /** The server decides whether a hint costs anything; for the ones offered here it does not. */
  showHint = () => {
    this.setState({ hintShown: true });
    if (this.props.onHintUsed) {
      this.props.onHintUsed();
    }
  };

  choose = option => {
    if (this.state.chosen !== null || this.props.submitting) {
      return;
    }

    this.setState({ chosen: option });
    this.props.onAnswer({ text: option });
  };

  render() {
    const { exercise, submitting } = this.props;
    const { chosen, hintShown } = this.state;
    const missingWord = exercise.type === ExerciseType.ContextToWordChoice;

    return (
      <div data-testid="choice-exercise">
        <div className="fs-4 fw-semibold">
          {/* Only when the prompt is the word; picking a word from its meaning must not be cued */}
          {exercise.type === ExerciseType.WordToMeaningChoice && <NounArticle article={exercise.article} />}
          <span data-testid="exercise-prompt">{exercise.prompt}</span>
        </div>
        {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}

        {/* The gap may need "frotte" while the options are "frotter" and the like */}
        {missingWord && (
          <p className="text-muted small mt-2 mb-0" data-testid="dictionary-form-note">
            The words are shown in their dictionary form.
          </p>
        )}

        {exercise.hint && (
          hintShown
            ? (
              <div className="text-muted fst-italic mt-2" data-testid="hint-text">
                <div>The missing word means: <span data-testid="hint-meaning">{exercise.hint}</span></div>
                {exercise.contextSentenceTranslation && (
                  <div data-testid="hint-translation">{exercise.contextSentenceTranslation}</div>
                )}
              </div>
            )
            : (
              <Button color="link" size="sm" className="ps-0 mt-1" data-testid="hint-button"
                      disabled={chosen !== null} onClick={this.showHint}>
                Show translations <span className="text-muted">(free)</span>
              </Button>
            )
        )}

        <div className="d-grid gap-2 mt-4">
          {exercise.options.map(option => (
            <Button
              key={option}
              color={chosen === option ? 'primary' : 'secondary'}
              outline={chosen !== option}
              className="text-start"
              disabled={submitting && chosen !== option}
              data-testid="choice-option"
              onClick={() => this.choose(option)}
            >
              {option}
            </Button>
          ))}
        </div>
      </div>
    );
  }
}
