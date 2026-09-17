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
    this.state = { chosen: null };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState({ chosen: null });
    }
  }

  choose = option => {
    if (this.state.chosen !== null || this.props.submitting) {
      return;
    }

    this.setState({ chosen: option });
    this.props.onAnswer({ text: option });
  };

  render() {
    const { exercise, submitting } = this.props;
    const { chosen } = this.state;

    return (
      <div data-testid="choice-exercise">
        <div className="fs-4 fw-semibold">
          {/* Only when the prompt is the word; picking a word from its meaning must not be cued */}
          {exercise.type === ExerciseType.WordToMeaningChoice && <NounArticle article={exercise.article} />}
          <span data-testid="exercise-prompt">{exercise.prompt}</span>
        </div>
        {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}

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
