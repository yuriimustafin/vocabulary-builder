import React from 'react';

/**
 * Puts stored forms back into the tables they were read from: mood, then tense, then one
 * row per person. The server returns them in page order, so grouping by first appearance
 * keeps "présent" ahead of "imparfait" without knowing French grammar here.
 */
export function groupForms(forms) {
  const moods = [];

  for (const { mood, tense, person, form } of forms) {
    const moodName = mood || 'other';
    let moodGroup = moods.find(m => m.mood === moodName);
    if (!moodGroup) {
      moodGroup = { mood: moodName, tenses: [] };
      moods.push(moodGroup);
    }

    const tenseName = tense || '';
    let tenseGroup = moodGroup.tenses.find(t => t.tense === tenseName);
    if (!tenseGroup) {
      tenseGroup = { tense: tenseName, rows: [] };
      moodGroup.tenses.push(tenseGroup);
    }

    tenseGroup.rows.push({ person, form });
  }

  return moods;
}

/**
 * The inflected forms of a word - for a verb, its conjugation.
 */
export function WordForms({ forms }) {
  if (!forms || forms.length === 0) {
    return null;
  }

  const moods = groupForms(forms);

  return (
    <div data-testid="word-forms">
      <h5 className="mt-4">Forms</h5>
      {moods.map(({ mood, tenses }) => (
        <div key={mood} className="mb-3" data-testid="word-forms-mood">
          <h6 className="text-uppercase text-muted small mb-2">{mood}</h6>
          <div className="row g-2">
            {tenses.map(({ tense, rows }) => (
              <div key={tense} className="col-6 col-md-4 col-lg-3">
                <div className="border rounded p-2 h-100">
                  {tense && <div className="fw-semibold small mb-1">{tense}</div>}
                  <table className="word-forms-table small mb-0">
                    <tbody>
                      {rows.map(({ person, form }, index) => (
                        <tr key={index}>
                          {person && <td className="text-muted">{person}</td>}
                          <td colSpan={person ? 1 : 2}>{form}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
