import React, { Component } from 'react';
import { Button, Input } from 'reactstrap';
import { NounArticle } from '../NounArticle';
import { ExerciseType } from './exerciseTypes';
import { FRENCH_ACCENTS } from './TypedExercise';

/**
 * Spelling the word while it is, or was a moment ago, in front of the learner.
 *
 * Copy: the word stays on screen and is typed out - its spelling met by hand first.
 *
 * Look, cover, write: the word is shown, covered, and typed from memory. A word the server
 * sends in pieces (more than five letters) goes a piece at a time: look at the highlighted
 * piece, cover it, type it, then the next - and the pieces typed are sent back as the word.
 * Marked on the server, which forgives case and the article; a slip here costs nothing.
 */
export class SpellingExercise extends Component {
  constructor(props) {
    super(props);
    this.state = this.emptyState();
    this.input = React.createRef();
  }

  emptyState() {
    return { text: '', covered: false, piece: 0, typed: [] };
  }

  componentDidMount() {
    this.focus();
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState(this.emptyState(), this.focus);
    }
  }

  get cover() {
    return this.props.exercise.type === ExerciseType.WordToSpellingCover;
  }

  /** The pieces a long word is covered in, or the whole word as one. */
  get pieces() {
    const { tiles, prompt } = this.props.exercise;
    return tiles && tiles.length > 1 ? tiles : [prompt];
  }

  focus = () => {
    if (this.input.current) {
      this.input.current.focus();
    }
  };

  coverIt = () => this.setState({ covered: true, text: '' }, this.focus);

  insert = letter => {
    const field = this.input.current;
    const { text } = this.state;
    const start = field ? field.selectionStart : text.length;
    const end = field ? field.selectionEnd : text.length;

    this.setState({ text: text.slice(0, start) + letter + text.slice(end) }, () => {
      if (field) {
        field.focus();
        field.setSelectionRange(start + letter.length, start + letter.length);
      }
    });
  };

  /** Copying, or the last piece covered: send the word. Otherwise on to the next piece. */
  next = () => {
    const { text, piece, typed } = this.state;

    if (text.trim().length === 0 || this.props.submitting) {
      return;
    }

    if (!this.cover) {
      this.props.onAnswer({ text });
      return;
    }

    const soFar = [...typed, text.trim()];

    if (piece + 1 < this.pieces.length) {
      this.setState({ typed: soFar, piece: piece + 1, covered: false, text: '' });
      return;
    }

    this.props.onAnswer({ text: soFar.join('') });
  };

  giveUp = () => this.props.onAnswer({ text: '', abandoned: true });

  handleKeyDown = event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.next();
    }
  };

  /** The word with the current piece marked: highlighted to look at, a gap once covered. */
  renderWord() {
    const { piece, covered, typed } = this.state;

    if (!this.cover) {
      return <span data-testid="spelling-word">{this.props.exercise.prompt}</span>;
    }

    return (
      <span data-testid="spelling-word">
        {this.pieces.map((part, index) => {
          if (index < piece) {
            return <span key={index} className="text-success">{typed[index]}</span>;
          }

          if (index === piece) {
            return covered
              ? <span key={index} className="text-muted" data-testid="covered-piece">{'_'.repeat(Math.max(2, part.length))}</span>
              : <mark key={index} className="px-0" data-testid="current-piece">{part}</mark>;
          }

          // Pieces still to come stay out of sight while one is being written
          return <span key={index} className={covered ? 'text-muted' : ''}>{covered ? '·'.repeat(part.length) : part}</span>;
        })}
      </span>
    );
  }

  render() {
    const { exercise, submitting, language } = this.props;
    const { text, covered, piece } = this.state;
    const writing = !this.cover || covered;
    const many = this.cover && this.pieces.length > 1;

    return (
      <div data-testid="spelling-exercise">
        <div className="fs-3 fw-semibold">
          <NounArticle article={exercise.article} />
          {this.renderWord()}
        </div>
        {exercise.transcription && <div className="text-muted">/{exercise.transcription}/</div>}
        {exercise.meaning && <div className="mt-1" data-testid="spelling-meaning">{exercise.meaning}</div>}

        {many && (
          <div className="text-muted small mt-2" data-testid="spelling-progress">
            Piece {piece + 1} of {this.pieces.length}
          </div>
        )}

        {!writing && (
          <div className="mt-4">
            <p className="text-muted small mb-2">
              {many ? 'Look at the highlighted piece, then cover it and write it.' : 'Look at the word, then cover it and write it.'}
            </p>
            <Button color="primary" data-testid="spelling-cover" onClick={this.coverIt}>Cover it</Button>
          </div>
        )}

        {writing && (
          <>
            <div className="mt-3">
              <Input
                innerRef={this.input}
                value={text}
                disabled={submitting}
                autoComplete="off"
                autoCorrect="off"
                autoCapitalize="off"
                spellCheck={false}
                lang={language}
                placeholder={this.cover ? (many ? 'Write this piece' : 'Write the word') : 'Type the word as shown'}
                data-testid="spelling-input"
                onChange={event => this.setState({ text: event.target.value })}
                onKeyDown={this.handleKeyDown}
              />
            </div>

            {language === 'fr' && (
              <div className="d-flex flex-wrap gap-1 mt-2" data-testid="accent-bar">
                {FRENCH_ACCENTS.map(letter => (
                  <Button key={letter} size="sm" color="secondary" outline disabled={submitting}
                          onMouseDown={event => event.preventDefault()} onClick={() => this.insert(letter)}>
                    {letter}
                  </Button>
                ))}
              </div>
            )}

            <div className="d-flex gap-2 mt-4">
              <Button color="primary" disabled={text.trim().length === 0 || submitting}
                      data-testid="spelling-submit" onClick={this.next}>
                {many && piece + 1 < this.pieces.length ? 'Next piece' : 'Check'} <span className="opacity-75 small">(enter)</span>
              </Button>
              <Button color="link" className="text-muted" disabled={submitting}
                      data-testid="spelling-give-up" onClick={this.giveUp}>
                I do not know
              </Button>
            </div>
          </>
        )}
      </div>
    );
  }
}
