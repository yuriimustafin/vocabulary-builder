import React from 'react';
import { Alert, Table } from 'reactstrap';

/**
 * The outcome of a LingQ or lesson-notes import.
 *
 * Both imports set terms aside - prose, questions, fixed expressions - and the list of
 * what was set aside is the part worth reading, so it is shown in full rather than
 * summarised: anything worth keeping can then be added by hand.
 */
export function ImportResultSummary({ result }) {
  if (!result) {
    return null;
  }

  const skipped = result.skipped || [];

  return (
    <>
      <Alert color="success" className="mt-3">
        <h5 className="alert-heading">Import complete</h5>
        <hr />
        <ul className="mb-0">
          <li><strong>{result.termsRead}</strong> terms read</li>
          <li><strong>{result.termsImported}</strong> resolved to a word</li>
          <li><strong>{result.wordsCreated}</strong> new words added</li>
          <li><strong>{result.encountersCreated}</strong> encounters recorded</li>
        </ul>
        {result.encountersCreated === 0 && result.termsImported > 0 && (
          <p className="mb-0 mt-2">
            Nothing new — every one of these was already recorded from this same import.
          </p>
        )}
        <p className="mb-0 mt-2 text-muted">
          Definitions are fetched from the dictionary later, when the words are exported.
        </p>
      </Alert>

      {skipped.length > 0 && (
        <Alert color="warning" className="mt-3">
          <h6 className="alert-heading">
            {skipped.length} term{skipped.length === 1 ? '' : 's'} not imported
          </h6>
          <p className="mb-2">
            These are sentences, questions or set phrases rather than vocabulary items.
            Add any you want to keep from the Bulk Import page.
          </p>
          <div style={{ maxHeight: '20rem', overflowY: 'auto' }}>
            <Table size="sm" borderless className="mb-0">
              <tbody>
                {skipped.map((item, index) => (
                  <tr key={`${item.sourceTerm}-${index}`}>
                    <td style={{ fontFamily: 'monospace' }}>{item.sourceTerm}</td>
                    <td className="text-muted">{item.reason}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        </Alert>
      )}
    </>
  );
}
