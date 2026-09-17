import React from 'react';

const GENDER_NAMES = {
  masculine: 'masculine',
  feminine: 'feminine',
  common: 'masculine or feminine'
};

/**
 * The article a noun is learned with, coloured by gender: "la", "le", "l'", "les".
 *
 * Renders nothing for a word without one, so it can sit in front of any headword. It is
 * deliberately a sibling of the headword rather than part of it - study answers are marked
 * against the headword alone, and tests read the headword's element as exact text.
 *
 * The elided "l'" and the plural "les" hide the gender in the text itself; the colour and
 * the tooltip still carry it.
 */
export function NounArticle({ article }) {
  if (!article) {
    return null;
  }

  const gender = GENDER_NAMES[article.gender] || article.gender;

  return (
    <>
      <span
        className={`noun-article noun-article-${article.gender}`}
        title={`${gender} noun`}
        data-testid="noun-article"
        data-gender={article.gender}
      >
        {article.definite}
      </span>
      {!article.isElided && ' '}
    </>
  );
}

/**
 * "un arbre" beside "l'arbre": when the definite article cannot show the gender, the
 * indefinite one can. Nothing is shown when "le" or "la" already says it.
 */
export function IndefiniteArticleHint({ article, headword }) {
  if (!article || !(article.isElided || article.isPlural)) {
    return null;
  }

  return (
    <span className="text-muted ms-2 fs-6 fw-normal" data-testid="indefinite-article">
      (<span className={`noun-article noun-article-${article.gender}`}>{article.indefinite}</span> {headword})
    </span>
  );
}
