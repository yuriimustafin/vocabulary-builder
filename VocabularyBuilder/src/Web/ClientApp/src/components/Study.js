import React, { Component } from 'react';
import { Alert, Badge, Card, CardBody, Spinner } from 'reactstrap';
import { componentFor } from './study/exerciseRegistry';
import { IntroductionCard } from './study/IntroductionCard';
import { AnswerFeedback } from './study/AnswerFeedback';
import { DevControls, isDevelopmentBuild } from './study/DevControls';
import { GRADE_KEYS } from './study/GradeBar';
import {
  CardDifficulty,
  EXERCISE_LABELS,
  GradingMode
} from './study/exerciseTypes';
import {
  NothingToStudy,
  PreparingWords,
  SessionProgress,
  StudyDone
} from './study/StudyStates';

const DIFFICULTY_BADGES = {
  [CardDifficulty.Shaky]: { label: 'Shaky', colour: 'warning' },
  [CardDifficulty.Difficult]: { label: 'Difficult', colour: 'danger' }
};

/**
 * One study session.
 *
 * The queue is fetched in a batch and worked through in order. Each card carries an
 * attempt id generated when it was rendered, which goes back with the answer, so a double
 * click or a retry after a timeout scores the card once.
 *
 * A graded answer can come back with follow-up exercises. Those are re-encoding rather
 * than assessment - they are played out before moving on and are never graded.
 */
export class Study extends Component {
  static displayName = Study.name;

  constructor(props) {
    super(props);

    this.state = {
      language: localStorage.getItem('language') || 'en',
      loading: true,
      refreshing: false,
      submitting: false,
      error: null,
      cards: [],
      index: 0,
      pendingEnrichment: 0,
      nextDueAtUtc: null,
      stats: null,
      hasAnyWords: true,
      followUps: [],
      followUpIndex: 0,
      feedback: null,
      hintUsed: false,
      revealed: false,
      shownAt: Date.now()
    };
  }

  componentDidMount() {
    document.addEventListener('keydown', this.handleKey);
    this.load();
  }

  componentWillUnmount() {
    document.removeEventListener('keydown', this.handleKey);
  }

  api(path) {
    return `/api/${this.state.language}/study${path}`;
  }

  async load(refreshing = false) {
    this.setState({ [refreshing ? 'refreshing' : 'loading']: true, error: null });

    try {
      // Fetched one after the other rather than together: the queue is what the session
      // needs and the stats only decorate it, and issuing both at once puts two concurrent
      // requests on a database that may be served by a single connection.
      const queueResponse = await fetch(this.api('/queue'));

      if (!queueResponse.ok) {
        throw new Error(`Queue request failed with ${queueResponse.status}`);
      }

      const queue = await queueResponse.json();

      const statsResponse = await fetch(this.api('/stats'));
      const stats = statsResponse.ok ? await statsResponse.json() : null;

      this.setState({
        loading: false,
        refreshing: false,
        cards: queue.cards,
        index: 0,
        pendingEnrichment: queue.pendingEnrichmentCount,
        nextDueAtUtc: queue.nextDueAtUtc,
        stats,
        // Nothing due, nothing waiting and nothing ever started means an empty collection
        // rather than a finished session.
        hasAnyWords: !stats || stats.notStarted > 0 || stats.learning > 0 || stats.young > 0 || stats.mature > 0,
        followUps: [],
        followUpIndex: 0,
        feedback: null,
        hintUsed: false,
        revealed: false,
        shownAt: Date.now()
      });
    } catch (error) {
      console.error('Could not load the study queue', error);
      this.setState({ loading: false, refreshing: false, error: 'Could not load the study queue.' });
    }
  }

  get currentCard() {
    return this.state.cards[this.state.index] || null;
  }

  get currentFollowUp() {
    return this.state.followUps[this.state.followUpIndex] || null;
  }

  /**
   * An answer is in flight, or the next batch is being fetched.
   *
   * The card stays on screen while the queue refetches, so without this a quick second
   * click lands on a card that is already being replaced - which at best submits twice and
   * at worst starts two overlapping requests.
   */
  get busy() {
    return this.state.submitting || this.state.refreshing;
  }

  /** A first showing, which is acknowledged rather than graded. */
  get isIntroduction() {
    return !this.currentFollowUp && !!this.currentCard && this.currentCard.isIntroduction;
  }

  handleKey = event => {
    if (this.busy || event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }

    if (this.state.feedback) {
      if (event.key === ' ' || event.key === 'Enter') {
        event.preventDefault();
        this.continueFromFeedback();
      }
      return;
    }

    if (this.isIntroduction) {
      if (event.key === ' ') {
        event.preventDefault();
        this.acknowledge();
      }
      return;
    }

    const exercise = this.currentFollowUp
      ? this.currentFollowUp.exercise
      : (this.currentCard && this.currentCard.exercise);

    if (!exercise || exercise.gradingMode !== GradingMode.SelfReported) {
      return;
    }

    if (event.key === ' ' && !this.state.revealed) {
      event.preventDefault();
      this.setState({ revealed: true });
      return;
    }

    const grade = GRADE_KEYS[event.key];
    if (grade && this.state.revealed) {
      event.preventDefault();
      this.grade(grade);
    }
  };

  acknowledge = async () => {
    const card = this.currentCard;

    if (!card || this.busy) {
      return;
    }

    this.setState({ submitting: true });

    try {
      const response = await fetch(this.api('/introductions'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cardId: card.cardId,
          attemptId: card.attemptId,
          exerciseType: card.exercise.type,
          elapsedMs: Date.now() - this.state.shownAt
        })
      });

      if (!response.ok) {
        throw new Error(`Could not record the introduction: ${response.status}`);
      }

      this.setState({ submitting: false }, this.nextCard);
    } catch (error) {
      console.error('Could not record the introduction', error);
      this.setState({ submitting: false, error: 'Could not record that word.' });
    }
  };

  /** Self-graded: the learner's own judgement goes straight through. */
  grade = selfGrade => this.submit({ selfGrade });

  /** Automatically graded: the server marks the answer against the word. */
  answer = ({ text, resets = 0, abandoned = false }) =>
    this.submit({ answer: text, resets, abandoned });

  async submit(payload) {
    const card = this.currentCard;
    if (!card || this.busy) {
      return;
    }

    // A follow-up is support, not assessment, so it takes a different path entirely.
    if (this.currentFollowUp) {
      return this.advanceFollowUp();
    }

    this.setState({ submitting: true });

    try {
      const response = await fetch(this.api('/reviews'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cardId: card.cardId,
          attemptId: card.attemptId,
          exerciseType: card.exercise.type,
          elapsedMs: Date.now() - this.state.shownAt,
          hintUsed: this.state.hintUsed,
          ...payload
        })
      });

      if (!response.ok) {
        throw new Error(`Review submission failed with ${response.status}`);
      }

      const result = await response.json();

      // An automatically graded answer comes back with the word to show. Holding it on
      // screen is the only chance to see what was marked and why, since unlike a self-graded
      // card the learner never revealed the answer themselves.
      if (result.feedback) {
        this.setState({
          submitting: false,
          feedback: result.feedback,
          pendingFollowUps: result.followUps || []
        });
        return;
      }

      if (result.followUps && result.followUps.length > 0) {
        this.setState({
          submitting: false,
          followUps: result.followUps,
          followUpIndex: 0,
          hintUsed: false,
          revealed: false,
          shownAt: Date.now()
        });
        return;
      }

      this.setState({ submitting: false }, this.nextCard);
    } catch (error) {
      console.error('Could not record the review', error);
      this.setState({ submitting: false, error: 'Could not record that answer.' });
    }
  }

  /** Dismisses the result, then plays out any re-encoding that was due to follow. */
  continueFromFeedback = () => {
    const followUps = this.state.pendingFollowUps || [];

    if (followUps.length > 0) {
      this.setState({
        feedback: null,
        pendingFollowUps: [],
        followUps,
        followUpIndex: 0,
        hintUsed: false,
        revealed: false,
        shownAt: Date.now()
      });
      return;
    }

    this.setState({ feedback: null, pendingFollowUps: [] }, this.nextCard);
  };

  /**
   * Records an ungraded re-encoding attempt and moves on. Failure here is not worth
   * interrupting the session for - the card has already been scored.
   */
  advanceFollowUp = async () => {
    const card = this.currentCard;
    const followUp = this.currentFollowUp;

    try {
      await fetch(this.api('/follow-ups'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cardId: card.cardId,
          attemptId: crypto.randomUUID(),
          exerciseType: followUp.exercise.type,
          elapsedMs: Date.now() - this.state.shownAt,
          correct: true
        })
      });
    } catch (error) {
      console.error('Could not record a follow-up attempt', error);
    }

    const nextIndex = this.state.followUpIndex + 1;

    if (nextIndex >= this.state.followUps.length) {
      this.setState({ followUps: [], followUpIndex: 0 }, this.nextCard);
      return;
    }

    this.setState({
      followUpIndex: nextIndex, feedback: null, hintUsed: false, revealed: false, shownAt: Date.now()
    });
  };

  nextCard = () => {
    const nextIndex = this.state.index + 1;

    // The end of the batch is not necessarily the end of the session: cards that were in
    // learning steps a few minutes ago may be due again.
    if (nextIndex >= this.state.cards.length) {
      this.load(true);
      return;
    }

    this.setState({
      index: nextIndex, feedback: null, hintUsed: false, revealed: false, shownAt: Date.now()
    });
  };

  renderExercise(exercise, isFollowUp) {
    if (this.isIntroduction) {
      return (
        <IntroductionCard
          exercise={exercise}
          submitting={this.busy}
          onAcknowledge={this.acknowledge}
        />
      );
    }

    const ExerciseComponent = componentFor(exercise.type);

    if (!ExerciseComponent) {
      return <Alert color="warning">This exercise type cannot be shown yet.</Alert>;
    }

    const shared = {
      exercise,
      submitting: this.busy,
      onHintUsed: () => this.setState({ hintUsed: true })
    };

    if (exercise.gradingMode === GradingMode.SelfReported) {
      return (
        <ExerciseComponent
          {...shared}
          revealed={this.state.revealed}
          onReveal={() => this.setState({ revealed: true })}
          // A follow-up is not scored, so its grade buttons only mean "move on".
          onGrade={isFollowUp ? this.advanceFollowUp : this.grade}
        />
      );
    }

    return (
      <ExerciseComponent
        {...shared}
        onAnswer={isFollowUp ? this.advanceFollowUp : this.answer}
      />
    );
  }

  render() {
    const {
      loading, refreshing, error, cards, index, pendingEnrichment, stats, hasAnyWords, nextDueAtUtc
    } = this.state;

    if (loading) {
      return <div className="text-center py-5"><Spinner color="primary" /></div>;
    }

    const card = this.currentCard;
    const followUp = this.currentFollowUp;

    return (
      <div style={{ maxWidth: '44rem' }}>
        <h1 className="h3 mb-4">Study</h1>

        {error && <Alert color="danger" data-testid="study-error">{error}</Alert>}

        {!card && pendingEnrichment > 0 && (
          <PreparingWords count={pendingEnrichment} refreshing={refreshing} onRefresh={() => this.load(true)} />
        )}

        {!card && pendingEnrichment === 0 && !hasAnyWords && <NothingToStudy />}

        {!card && pendingEnrichment === 0 && hasAnyWords && (
          <StudyDone stats={stats} nextDueAtUtc={nextDueAtUtc} onRefresh={() => this.load(true)} />
        )}

        {card && (
          <>
            <SessionProgress position={index} total={cards.length} stats={stats} />

            <Card data-testid="study-card">
              <CardBody className="p-4">
                <div className="d-flex justify-content-between align-items-center mb-3">
                  <span className="text-muted small text-uppercase" data-testid="exercise-label">
                    {this.state.feedback
                      ? 'Result'
                      : followUp
                        ? 'Let us go over it'
                        : card.isIntroduction
                          ? 'New word'
                          : EXERCISE_LABELS[card.exercise.type] || 'Review'}
                  </span>
                  <span className="d-flex gap-2">
                    {card.isNew && <Badge color="primary" pill data-testid="new-badge">New</Badge>}
                    {DIFFICULTY_BADGES[card.difficulty] && !followUp && (
                      <Badge color={DIFFICULTY_BADGES[card.difficulty].colour} pill data-testid="difficulty-badge">
                        {DIFFICULTY_BADGES[card.difficulty].label}
                      </Badge>
                    )}
                  </span>
                </div>

                {this.state.feedback
                  ? (
                    <AnswerFeedback
                      feedback={this.state.feedback}
                      continuing={this.busy}
                      onContinue={this.continueFromFeedback}
                    />
                  )
                  : followUp
                    ? this.renderExercise(followUp.exercise, true)
                    : this.renderExercise(card.exercise, false)}
              </CardBody>
            </Card>

            {followUp && (
              <p className="text-muted small mt-3 mb-0" data-testid="follow-up-note">
                Going over a word you missed. This one is not scored.
              </p>
            )}
          </>
        )}

        {isDevelopmentBuild && (
          <DevControls language={this.state.language} onCleared={() => this.load(true)} />
        )}
      </div>
    );
  }
}
