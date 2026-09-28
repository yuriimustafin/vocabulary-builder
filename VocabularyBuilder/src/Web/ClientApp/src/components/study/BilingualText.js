import React from 'react';

/**
 * Flags for the two sides of a bilingual entry.
 *
 * Presentation only. The database keeps the two texts as plain fields, and it is here that
 * they are labelled - so nothing that compares meanings, exports them or feeds them to a
 * model ever has a flag in its data.
 */
const FLAGS = {
  fr: '🇫🇷',
  en: '🇬🇧'
};

function Line({ flag, children, testId }) {
  return (
    <div className="d-flex gap-2" data-testid={testId}>
      <span aria-hidden="true">{flag}</span>
      <span>{children}</span>
    </div>
  );
}

/**
 * A meaning, or an example sentence, shown on one line per language.
 *
 * `learned` is the language being studied and comes first, because that is the side the
 * learner is here for; `native` is the translation. Either may be missing - a word with no
 * gloss, or a sentence nobody translated - and only the line that has text is drawn, so a
 * single-language entry looks deliberate rather than half-empty.
 */
export function BilingualText({ learned, native, language = 'fr', className, learnedTestId, nativeTestId }) {
  const hasLearned = learned && learned.trim();
  const hasNative = native && native.trim();

  if (!hasLearned && !hasNative) {
    return null;
  }

  // One language, no flag: there is nothing to tell apart
  if (!hasLearned || !hasNative) {
    return (
      <div className={className} data-testid={hasLearned ? learnedTestId : nativeTestId}>
        {hasLearned ? learned : native}
      </div>
    );
  }

  return (
    <div className={className}>
      <Line flag={FLAGS[language] || FLAGS.fr} testId={learnedTestId}>{learned}</Line>
      <Line flag={FLAGS.en} testId={nativeTestId}>{native}</Line>
    </div>
  );
}
