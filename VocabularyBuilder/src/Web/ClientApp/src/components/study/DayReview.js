import React, { Component } from 'react';
import { Badge, Button, Card, CardBody } from 'reactstrap';
import { NounArticle } from '../NounArticle';

function shuffled(items) {
  return items
    .map(item => ({ item, key: Math.random() }))
    .sort((a, b) => a.key - b.key)
    .map(({ item }) => item);
}

/**
 * The day's new words once more before it ends: each group's sentences on one side and
 * its words on the other, matched by picking a sentence and then the word that fills it.
 *
 * Practice only - nothing here is sent back, and nothing about any word's schedule moves.
 */
export class DayReview extends Component {
  constructor(props) {
    super(props);
    this.state = this.groupState(0);
  }

  groupState(groupIndex) {
    const group = this.props.groups[groupIndex];

    return {
      groupIndex,
      sentences: group ? shuffled(group.pairs) : [],
      words: group ? shuffled(group.pairs) : [],
      selected: null,
      matched: [],
      wrong: null,
      mistakes: groupIndex === 0 ? 0 : this.state.mistakes
    };
  }

  selectSentence = pair => {
    if (!this.state.matched.includes(pair.wordId)) {
      this.setState({ selected: pair.wordId, wrong: null });
    }
  };

  selectWord = pair => {
    const { selected, matched } = this.state;

    if (selected === null || matched.includes(pair.wordId)) {
      return;
    }

    if (pair.wordId === selected) {
      this.setState({ matched: [...matched, selected], selected: null, wrong: null });
    } else {
      this.setState(state => ({ wrong: pair.wordId, mistakes: state.mistakes + 1 }));
    }
  };

  nextGroup = () => this.setState(this.groupState(this.state.groupIndex + 1));

  render() {
    const { groups, onDone } = this.props;
    const { groupIndex, sentences, words, selected, matched, wrong, mistakes } = this.state;

    if (groupIndex >= groups.length) {
      return (
        <Card className="text-center" data-testid="day-review-finished">
          <CardBody className="py-5">
            <h5>That is today's words matched</h5>
            <p className="text-muted">
              {mistakes === 0 ? 'Every one first time.' : `${mistakes} ${mistakes === 1 ? 'mix-up' : 'mix-ups'} on the way.`}
            </p>
            <Button color="primary" outline onClick={onDone} data-testid="day-review-close">Done</Button>
          </CardBody>
        </Card>
      );
    }

    const groupDone = matched.length === sentences.length;

    return (
      <Card data-testid="day-review">
        <CardBody className="p-4">
          <div className="d-flex justify-content-between align-items-center mb-3">
            <span className="text-muted small text-uppercase">Today's words</span>
            <Badge color="secondary" pill data-testid="day-review-progress">
              Group {groupIndex + 1} of {groups.length}
            </Badge>
          </div>
          <p className="text-muted small">Pick a sentence, then the word that fills its gap.</p>

          <div className="row g-3">
            <div className="col-md-8 d-grid gap-2">
              {sentences.map(pair => (
                <Button
                  key={pair.wordId}
                  color={matched.includes(pair.wordId) ? 'success' : selected === pair.wordId ? 'primary' : 'secondary'}
                  outline={!matched.includes(pair.wordId) && selected !== pair.wordId}
                  className="text-start"
                  disabled={matched.includes(pair.wordId)}
                  data-testid="day-review-sentence"
                  data-word-id={pair.wordId}
                  onClick={() => this.selectSentence(pair)}
                >
                  {pair.sentence}
                  {pair.translation && <div className="small opacity-75 fst-italic">{pair.translation}</div>}
                </Button>
              ))}
            </div>
            <div className="col-md-4 d-grid gap-2 align-content-start">
              {words.map(pair => (
                <Button
                  key={pair.wordId}
                  color={matched.includes(pair.wordId) ? 'success' : wrong === pair.wordId ? 'danger' : 'secondary'}
                  outline={!matched.includes(pair.wordId)}
                  disabled={matched.includes(pair.wordId) || selected === null}
                  data-testid="day-review-word"
                  data-word-id={pair.wordId}
                  onClick={() => this.selectWord(pair)}
                >
                  <NounArticle article={pair.article} />{pair.form || pair.headword}
                </Button>
              ))}
            </div>
          </div>

          {groupDone && (
            <Button color="primary" className="mt-4" onClick={this.nextGroup} data-testid="day-review-next">
              {groupIndex + 1 < groups.length ? 'Next group' : 'Finish'}
            </Button>
          )}
        </CardBody>
      </Card>
    );
  }
}
