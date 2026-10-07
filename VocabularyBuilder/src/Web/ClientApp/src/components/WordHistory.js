import React, { useEffect, useState } from 'react';
import { Badge } from 'reactstrap';
import { Link } from 'react-router-dom';
import { currentLanguage, formatDate, formatDateTime, humanize, kindLabel } from './historyFormat';

/**
 * The imports a word came in through and what has been done to it since, for the word
 * details popup. Fetched on its own, so the rest of the details never waits for it.
 */
export function WordHistory({ wordId }) {
  const [history, setHistory] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setHistory(null);

    fetch(`/api/${currentLanguage()}/history/words/${wordId}`)
      .then(response => (response.ok ? response.json() : null))
      .then(data => { if (!cancelled) setHistory(data); })
      .catch(() => { if (!cancelled) setHistory(null); });

    return () => { cancelled = true; };
  }, [wordId]);

  if (!history) {
    return null;
  }

  const { imports, activity, calls, reviews } = history;

  return (
    <>
      {imports.length > 0 && (
        <>
          <h5 className="mt-4">Imports ({imports.length})</h5>
          <ul data-testid="word-imports">
            {imports.map(item => (
              <li key={item.importId}>
                <Link to={`/imports?open=${item.importId}`}>
                  {kindLabel(item.kind)}{item.name ? `: ${item.name}` : ''}
                </Link>{' '}
                <small className="text-muted">{formatDate(item.startedAtUtc)}</small>
                {item.createdWord && <Badge color="success" className="ms-2">added the word</Badge>}
                {item.sourceTerms.length > 0 && (
                  <small className="text-muted ms-2">as {item.sourceTerms.map(t => `“${t}”`).join(', ')}</small>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      {reviews.length > 0 && (
        <>
          <h5 className="mt-4">Study answers ({reviews.length})</h5>
          <div className="table-responsive">
            <table className="table table-sm" data-testid="word-reviews">
              <tbody>
                {reviews.slice(0, 10).map(review => (
                  <tr key={review.id} className={review.voidedAtUtc ? 'text-muted' : ''}>
                    <td><small>{formatDateTime(review.reviewedAtUtc)}</small></td>
                    <td>{humanize(review.exerciseType)}</td>
                    <td>{review.answer && <span style={{ fontFamily: 'monospace' }}>{review.answer}</span>}</td>
                    <td>
                      {review.grade}
                      {review.voidedAtUtc && <Badge color="secondary" className="ms-1">voided</Badge>}
                    </td>
                    <td><small>level {review.rungBefore} → {review.rungAfter}</small></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {reviews.length > 10 && (
            <p><Link to={`/history?tab=reviews&wordId=${wordId}`}>All {reviews.length} answers</Link></p>
          )}
        </>
      )}

      {(activity.length > 0 || calls.length > 0) && (
        <>
          <h5 className="mt-4">History</h5>
          {activity.length > 0 && (
            <div className="table-responsive">
              <table className="table table-sm" data-testid="word-activity">
                <tbody>
                  {activity.map(entry => (
                    <tr key={entry.id}>
                      <td><small>{formatDateTime(entry.occurredAtUtc)}</small></td>
                      <td>{humanize(entry.action)}</td>
                      <td>{entry.summary}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {calls.length > 0 && (
            <p className="mb-0">
              <Link to={`/history?tab=calls&wordId=${wordId}`}>
                {calls.length} model/dictionary call{calls.length === 1 ? '' : 's'}
              </Link>{' '}
              <small className="text-muted">
                ({calls.filter(c => !c.succeeded).length} failed)
              </small>
            </p>
          )}
        </>
      )}
    </>
  );
}
