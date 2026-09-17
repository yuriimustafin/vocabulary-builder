import React from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, CardBody, Progress, Spinner } from 'reactstrap';

/**
 * Shown when today's words are picked but not yet ready to study.
 *
 * Nothing pushes from the server, by design - the page simply offers to look again, which
 * is enough for a wait measured in seconds.
 */
export function PreparingWords({ count, onRefresh, refreshing }) {
  return (
    <Card className="text-center" data-testid="preparing-words">
      <CardBody className="py-5">
        <Spinner color="primary" className="mb-3" />
        <h5>Preparing {count} {count === 1 ? 'word' : 'words'}</h5>
        <p className="text-muted">
          Definitions and examples are being written for words that did not have them.
          This usually takes a few seconds.
        </p>
        <Button color="primary" outline onClick={onRefresh} disabled={refreshing} data-testid="preparing-refresh">
          {refreshing ? 'Checking…' : 'Check again'}
        </Button>
      </CardBody>
    </Card>
  );
}

/** Nothing due and nothing left of today's new words. */
export function StudyDone({ stats, nextDueAtUtc, onRefresh }) {
  return (
    <Card className="text-center" data-testid="study-done">
      <CardBody className="py-5">
        <h5>Nothing due right now</h5>
        <p className="text-muted mb-4" data-testid="done-detail">
          {describeWait(stats, nextDueAtUtc)}
        </p>
        <div className="d-flex gap-2 justify-content-center">
          <Button color="primary" outline onClick={onRefresh} data-testid="done-refresh">Check again</Button>
          <Button color="secondary" outline tag={Link} to="/words">Browse words</Button>
        </div>
      </CardBody>
    </Card>
  );
}

/**
 * Says how long the wait is rather than implying the day is over. A step measured in
 * minutes is a pause, not a finish, and the two should not read the same.
 */
function describeWait(stats, nextDueAtUtc) {
  const minutes = nextDueAtUtc
    ? Math.round((Date.parse(nextDueAtUtc.endsWith('Z') ? nextDueAtUtc : `${nextDueAtUtc}Z`) - Date.now()) / 60000)
    : null;

  if (minutes !== null && minutes > 0 && minutes < 90) {
    return `The next word is ready in about ${minutes} ${minutes === 1 ? 'minute' : 'minutes'}.`;
  }

  if (stats && stats.newToday >= stats.newCardsPerDay) {
    return `That is today's ${stats.newCardsPerDay} new words done, and every step cleared. `
      + 'The next review falls due later.';
  }

  return 'Every word that was due has been reviewed.';
}

/** Nothing to study because nothing has been collected yet. */
export function NothingToStudy() {
  return (
    <Alert color="info" data-testid="nothing-to-study">
      <h6>No words to study yet</h6>
      <p className="mb-2">Add some words first and they will start appearing here.</p>
      <Button color="primary" outline size="sm" tag={Link} to="/words">Go to words</Button>
    </Alert>
  );
}

/** Where the session is: how much is left, and how much of today's allowance is used. */
export function SessionProgress({ position, total, stats }) {
  const percent = total === 0 ? 0 : Math.round((position / total) * 100);

  return (
    <div className="mb-4" data-testid="session-progress">
      <div className="d-flex justify-content-between small text-muted mb-1">
        <span>Card {Math.min(position + 1, total)} of {total}</span>
        {stats && <span data-testid="new-today">New today: {stats.newToday} / {stats.newCardsPerDay}</span>}
      </div>
      <Progress value={percent} style={{ height: '4px' }} />
    </div>
  );
}
