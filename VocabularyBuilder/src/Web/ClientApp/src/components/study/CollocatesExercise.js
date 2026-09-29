import React, { Component } from 'react';
import { Button } from 'reactstrap';
import { NounArticle } from '../NounArticle';

/**
 * "What can be bright?" - the word, and a handful of words to tick the ones it goes with.
 *
 * Any number may be right. The server knows which were, so nothing here marks them; the
 * result screen lists the right ones afterwards either way.
 */
export class CollocatesExercise extends Component {
  constructor(props) {
    super(props);
    this.state = { ticked: [] };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState({ ticked: [] });
    }
  }

  toggle = option => {
    this.setState(state => ({
      ticked: state.ticked.includes(option)
        ? state.ticked.filter(t => t !== option)
        : [...state.ticked, option]
    }));
  };

  submit = () => this.props.onAnswer({ selections: this.state.ticked });

  giveUp = () => this.props.onAnswer({ selections: [], abandoned: true });

  render() {
    const { exercise, submitting } = this.props;
    const { ticked } = this.state;

    return (
      <div data-testid="collocates-exercise">
        <p className="text-muted small mb-1">Which of these go with</p>
        <div className="fs-4 fw-semibold">
          <NounArticle article={exercise.article} />
          <span data-testid="exercise-prompt">{exercise.prompt}</span>
          <span className="text-muted">?</span>
        </div>
        {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}

        <div className="d-flex flex-wrap gap-2 mt-4">
          {exercise.options.map(option => (
            <Button
              key={option}
              color={ticked.includes(option) ? 'primary' : 'secondary'}
              outline={!ticked.includes(option)}
              disabled={submitting}
              aria-pressed={ticked.includes(option)}
              data-testid="collocate-option"
              onClick={() => this.toggle(option)}
            >
              {option}
            </Button>
          ))}
        </div>

        <div className="d-flex gap-2 mt-4">
          <Button color="primary" disabled={ticked.length === 0 || submitting}
                  data-testid="collocates-submit" onClick={this.submit}>
            Check
          </Button>
          <Button color="link" className="text-muted" disabled={submitting}
                  data-testid="collocates-give-up" onClick={this.giveUp}>
            I do not know
          </Button>
        </div>
      </div>
    );
  }
}
