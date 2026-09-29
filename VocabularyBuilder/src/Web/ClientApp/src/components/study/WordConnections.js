import React from 'react';

/**
 * What ties a word to things already known: what it is used about, where it comes from,
 * words it is related to, and something it sounds like.
 *
 * Each is another route back to the word. The mnemonic is support for the first tries and
 * for a miss - once the word comes back on its own it has done its job - so a caller can
 * leave it out.
 */
export function WordConnections({ connections, showMnemonic = true, className = 'mt-3' }) {
  if (!connections) {
    return null;
  }

  const rows = [
    { key: 'usage', label: 'Used for', text: connections.usage },
    { key: 'etymology', label: 'Origin', text: connections.etymology },
    { key: 'cognates', label: 'Related', text: connections.cognates },
    { key: 'mnemonic', label: 'Sounds like', text: showMnemonic ? connections.mnemonic : null }
  ].filter(row => row.text);

  if (rows.length === 0) {
    return null;
  }

  return (
    <dl className={`small mb-0 ${className}`} data-testid="word-connections">
      {rows.map(row => (
        <div key={row.key} className="d-flex gap-2">
          <dt className="text-muted fw-normal text-nowrap" style={{ minWidth: '6.5rem' }}>{row.label}</dt>
          <dd className="mb-1" data-testid={`connection-${row.key}`}>{row.text}</dd>
        </div>
      ))}
    </dl>
  );
}
