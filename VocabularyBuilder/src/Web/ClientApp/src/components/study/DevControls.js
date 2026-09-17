import React, { Component } from 'react';
import { Button, Spinner } from 'reactstrap';

/**
 * Throwing the session away to try it again.
 *
 * Only rendered outside a production build, and the endpoints behind it only exist outside
 * a production environment - either alone would do, but this deletes rows, so it is worth
 * both.
 *
 * Words are never touched, and neither is any content generated for them: that cost a model
 * call, and clearing it would mean paying for it twice.
 */
export class DevControls extends Component {
  constructor(props) {
    super(props);
    this.state = { working: null, result: null, error: null };
  }

  clear = async scope => {
    if (scope === 'all' && !window.confirm(
      'Delete every study card and review for this language? Words themselves are kept.')) {
      return;
    }

    this.setState({ working: scope, result: null, error: null });

    try {
      const response = await fetch(`/api/${this.props.language}/study/dev/clear-${scope}`, {
        method: 'POST'
      });

      if (!response.ok) {
        throw new Error(`Clear failed with ${response.status}`);
      }

      this.setState({ working: null, result: await response.json() });
      this.props.onCleared();
    } catch (error) {
      console.error('Could not clear study progress', error);
      this.setState({ working: null, error: 'Could not clear study progress.' });
    }
  };

  render() {
    const { working, result, error } = this.state;

    return (
      <div className="border rounded p-3 mt-4 bg-light" data-testid="dev-controls">
        <div className="text-muted small text-uppercase mb-2">Development only</div>

        <div className="d-flex gap-2 flex-wrap align-items-center">
          <Button
            size="sm"
            color="secondary"
            outline
            disabled={working !== null}
            data-testid="dev-clear-today"
            onClick={() => this.clear('today')}
          >
            Restart today
          </Button>

          <Button
            size="sm"
            color="danger"
            outline
            disabled={working !== null}
            data-testid="dev-clear-all"
            onClick={() => this.clear('all')}
          >
            Clear all progress
          </Button>

          {working && <Spinner size="sm" color="secondary" />}
        </div>

        {result && (
          <div className="text-muted small mt-2" data-testid="dev-clear-result">
            Removed {result.cardsRemoved} {result.cardsRemoved === 1 ? 'card' : 'cards'} and{' '}
            {result.reviewsRemoved} {result.reviewsRemoved === 1 ? 'review' : 'reviews'}.
            {result.cardsLeftAdvanced > 0 && (
              <>
                {' '}
                {result.cardsLeftAdvanced}{' '}
                {result.cardsLeftAdvanced === 1 ? 'older word was' : 'older words were'} answered
                today and kept the state those answers left; clear all progress to reset those too.
              </>
            )}
          </div>
        )}

        {error && <div className="text-danger small mt-2" data-testid="dev-clear-error">{error}</div>}
      </div>
    );
  }
}

/** True in a development build, where these controls are safe to offer. */
export const isDevelopmentBuild = process.env.NODE_ENV === 'development';
