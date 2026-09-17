import React, { Component } from 'react';
import { Button } from 'reactstrap';

/**
 * Spelling from tiles: the meaning is shown and the word is rebuilt letter by letter.
 *
 * Starting over is counted and sent with the answer. Needing to reset means the spelling
 * was not actually known, so the server grades it down however quickly it finished.
 */
export class ScrambleExercise extends Component {
  constructor(props) {
    super(props);
    this.state = this.emptyState(props.exercise);
  }

  emptyState(exercise) {
    return {
      // Tiles are tracked by position, since a word can use the same letter twice.
      placed: [],
      available: exercise.tiles.map((letter, index) => ({ letter, index })),
      resets: 0
    };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState(this.emptyState(this.props.exercise));
    }
  }

  place = tile => {
    this.setState(state => ({
      placed: [...state.placed, tile],
      available: state.available.filter(candidate => candidate.index !== tile.index)
    }));
  };

  takeBack = tile => {
    this.setState(state => ({
      placed: state.placed.filter(candidate => candidate.index !== tile.index),
      available: [...state.available, tile]
    }));
  };

  /**
   * Takes back the last letter placed.
   *
   * Not counted as starting over: correcting a letter is part of spelling a word, whereas
   * clearing the lot means the spelling was not known. Only the latter should cost the
   * grade.
   */
  deleteLast = () => {
    this.setState(state => {
      if (state.placed.length === 0) {
        return null;
      }

      const last = state.placed[state.placed.length - 1];

      return {
        placed: state.placed.slice(0, -1),
        available: [...state.available, last]
      };
    });
  };

  reset = () => {
    this.setState(state => ({
      ...this.emptyState(this.props.exercise),
      resets: state.resets + 1
    }));
  };

  submit = () => {
    this.props.onAnswer({
      text: this.state.placed.map(tile => tile.letter).join(''),
      resets: this.state.resets
    });
  };

  giveUp = () => this.props.onAnswer({ text: '', abandoned: true, resets: this.state.resets });

  render() {
    const { exercise, submitting } = this.props;
    const { placed, available } = this.state;

    return (
      <div data-testid="scramble-exercise">
        <div className="fs-5" data-testid="exercise-prompt">{exercise.prompt}</div>

        <div className="border rounded p-3 mt-3 d-flex flex-wrap gap-2 bg-light" style={{ minHeight: '3.5rem' }}
             data-testid="scramble-answer">
          {placed.map(tile => (
            <Button key={tile.index} size="sm" color="primary" data-testid="placed-tile"
                    onClick={() => this.takeBack(tile)}>
              {tile.letter}
            </Button>
          ))}
          {placed.length === 0 && <span className="text-muted align-self-center">Tap the letters in order</span>}
        </div>

        <div className="d-flex flex-wrap gap-2 mt-3" data-testid="scramble-tiles">
          {available.map(tile => (
            <Button key={tile.index} color="secondary" outline data-testid="scramble-tile"
                    onClick={() => this.place(tile)}>
              {tile.letter}
            </Button>
          ))}
        </div>

        <div className="d-flex gap-2 mt-4">
          <Button color="primary" disabled={placed.length === 0 || submitting}
                  data-testid="scramble-submit" onClick={this.submit}>
            Check
          </Button>
          <Button color="secondary" outline disabled={placed.length === 0 || submitting}
                  data-testid="scramble-delete" onClick={this.deleteLast}>
            Delete
          </Button>
          <Button color="secondary" outline disabled={placed.length === 0 || submitting}
                  data-testid="scramble-reset" onClick={this.reset}>
            Start over
          </Button>
          <Button color="link" className="text-muted" disabled={submitting}
                  data-testid="scramble-give-up" onClick={this.giveUp}>
            I do not know
          </Button>
        </div>
      </div>
    );
  }
}
