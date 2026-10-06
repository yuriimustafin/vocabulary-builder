import React, { Component } from 'react';
import { Button, Input } from 'reactstrap';
import { NounArticle } from '../NounArticle';

// The letters a French keyboard layout has and most others do not.
export const FRENCH_ACCENTS = ['é', 'è', 'ê', 'ë', 'à', 'â', 'ç', 'ù', 'û', 'ü', 'ô', 'î', 'ï', 'œ'];

/**
 * The word, typed from its meaning - with the first letters shown when the exercise
 * carries a cue.
 *
 * Marked on the server, which forgives case, a missing accent and a single slipped letter,
 * so there is nothing here but the input. French gets a row of accented letters, since a
 * missing accent costs the answer its full credit and most keyboards make them awkward.
 */
export class TypedExercise extends Component {
  constructor(props) {
    super(props);
    this.state = { text: '' };
    this.input = React.createRef();
  }

  componentDidMount() {
    this.focus();
  }

  componentDidUpdate(previous) {
    if (previous.exercise !== this.props.exercise) {
      this.setState({ text: '' }, this.focus);
    }
  }

  focus = () => {
    if (this.input.current) {
      this.input.current.focus();
    }
  };

  /** Puts the letter where the cursor is, not at the end, and keeps the cursor after it. */
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

  submit = () => {
    if (this.state.text.trim().length === 0 || this.props.submitting) {
      return;
    }

    this.props.onAnswer({ text: this.state.text });
  };

  giveUp = () => this.props.onAnswer({ text: '', abandoned: true });

  handleKeyDown = event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.submit();
    }
  };

  render() {
    const { exercise, submitting, language } = this.props;
    const { text } = this.state;

    return (
      <div data-testid="typed-exercise">
        <div className="fs-5" data-testid="exercise-prompt">{exercise.prompt}</div>
        {exercise.partOfSpeech && (
          <div className="text-muted small fst-italic">{exercise.partOfSpeech}</div>
        )}

        {exercise.letterMask && (
          <div className="mt-3">
            <code className="fs-3 letter-mask" data-testid="letter-mask">{exercise.letterMask}</code>
          </div>
        )}

        <div className="d-flex align-items-center gap-2 mt-3">
          <NounArticle article={exercise.article} />
          <Input
            innerRef={this.input}
            value={text}
            disabled={submitting}
            autoComplete="off"
            autoCorrect="off"
            autoCapitalize="off"
            spellCheck={false}
            lang={language}
            placeholder="Type the word"
            data-testid="typed-input"
            onChange={event => this.setState({ text: event.target.value })}
            onKeyDown={this.handleKeyDown}
          />
        </div>

        {language === 'fr' && (
          <div className="d-flex flex-wrap gap-1 mt-2" data-testid="accent-bar">
            {FRENCH_ACCENTS.map(letter => (
              <Button
                key={letter}
                size="sm"
                color="secondary"
                outline
                disabled={submitting}
                data-testid="accent-key"
                // Keeps the input focused, so the cursor position is still there to insert at.
                onMouseDown={event => event.preventDefault()}
                onClick={() => this.insert(letter)}
              >
                {letter}
              </Button>
            ))}
          </div>
        )}

        <div className="d-flex gap-2 mt-4">
          <Button color="primary" disabled={text.trim().length === 0 || submitting}
                  data-testid="typed-submit" onClick={this.submit}>
            Check <span className="opacity-75 small">(enter)</span>
          </Button>
          <Button color="link" className="text-muted" disabled={submitting}
                  data-testid="typed-give-up" onClick={this.giveUp}>
            I do not know
          </Button>
        </div>
      </div>
    );
  }
}
