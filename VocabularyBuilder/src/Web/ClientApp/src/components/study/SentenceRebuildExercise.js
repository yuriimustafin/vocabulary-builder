import React, { Component } from 'react';
import { Button } from 'reactstrap';

/**
 * One of the word's sentences, given as written but for a gap: the word and the few words
 * around it, shuffled as tiles, to be put back in order in the gap.
 *
 * What the sentence says, and what each tile means, are a free hint. Starting over is counted
 * and sent with the answer, as for the word's own pieces; a slip in the order costs nothing.
 */
export class SentenceRebuildExercise extends Component {
  constructor(props) {
    super(props);
    this.state = this.emptyState(props.exercise);
  }

  emptyState(exercise) {
    return {
      // By position, since two tiles can read the same
      placed: [],
      available: exercise.tiles.map((text, index) => ({ text, index })),
      resets: 0,
      hintShown: false
    };
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState(this.emptyState(this.props.exercise));
    }
  }

  place = tile => this.setState(state => ({
    placed: [...state.placed, tile],
    available: state.available.filter(candidate => candidate.index !== tile.index)
  }));

  takeBack = tile => this.setState(state => ({
    placed: state.placed.filter(candidate => candidate.index !== tile.index),
    available: [...state.available, tile]
  }));

  reset = () => this.setState(state => ({
    ...this.emptyState(this.props.exercise),
    resets: state.resets + 1,
    hintShown: state.hintShown
  }));

  showHint = () => {
    this.setState({ hintShown: true });
    if (this.props.onHintUsed) {
      this.props.onHintUsed();
    }
  };

  submit = () => this.props.onAnswer({
    text: this.state.placed.map(tile => tile.text).join(' '),
    resets: this.state.resets
  });

  giveUp = () => this.props.onAnswer({ text: '', abandoned: true, resets: this.state.resets });

  render() {
    const { exercise, submitting } = this.props;
    const { placed, available, hintShown } = this.state;
    const hints = exercise.optionHints || {};
    const hasHint = exercise.contextSentenceTranslation || Object.keys(hints).length > 0;
    const complete = available.length === 0;

    return (
      <div data-testid="sentence-exercise">
        <div className="fs-5 lh-lg" data-testid="exercise-prompt">
          <span data-testid="sentence-start">{exercise.sentenceStart}</span>
          <span className="d-inline-flex flex-wrap gap-1 align-middle border-bottom border-2 px-1 mx-1"
                style={{ minWidth: '6rem', minHeight: '2.2rem' }} data-testid="sentence-gap">
            {placed.map(tile => (
              <Button key={tile.index} size="sm" color="primary" data-testid="placed-tile"
                      onClick={() => this.takeBack(tile)}>
                {tile.text}
              </Button>
            ))}
          </span>
          <span data-testid="sentence-end">{exercise.sentenceEnd}</span>
        </div>

        {hasHint && (
          hintShown
            ? (
              <div className="text-muted fst-italic small mt-2" data-testid="hint-text">
                {exercise.contextSentenceTranslation && <div>{exercise.contextSentenceTranslation}</div>}
                {exercise.tiles.filter(tile => hints[tile]).map(tile => (
                  <div key={tile} data-testid="tile-hint"><span className="fw-semibold">{tile}</span>: {hints[tile]}</div>
                ))}
              </div>
            )
            : (
              <Button color="link" size="sm" className="ps-0 mt-1" data-testid="hint-button"
                      disabled={submitting} onClick={this.showHint}>
                Show translations <span className="text-muted">(free)</span>
              </Button>
            )
        )}

        <div className="d-flex flex-wrap gap-2 mt-3" data-testid="scramble-tiles">
          {available.map(tile => (
            <Button key={tile.index} color="secondary" outline data-testid="scramble-tile"
                    onClick={() => this.place(tile)}>
              {tile.text}
            </Button>
          ))}
        </div>

        <div className="d-flex gap-2 mt-4">
          <Button color="primary" disabled={!complete || submitting}
                  data-testid="scramble-submit" onClick={this.submit}>
            Check
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
