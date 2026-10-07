/**
 * Small helpers shared by the Imports and History pages and the word's own history.
 */

/**
 * The API sends UTC times without a zone - they are stored that way, as DateTime - and a
 * browser reads a zoneless time as local. Marked as UTC here, so they display in the
 * viewer's own time rather than hours off.
 */
export function parseUtc(value) {
  if (!value) {
    return null;
  }

  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
}

export function formatDateTime(value) {
  const date = parseUtc(value);
  return date ? date.toLocaleString() : '-';
}

export function formatDate(value) {
  const date = parseUtc(value);
  return date ? date.toLocaleDateString() : '-';
}

const KIND_LABELS = {
  BulkList: 'Bulk',
  Kindle: 'Kindle',
  LingQ: 'LingQ',
  LessonNotes: 'Notes'
};

export function kindLabel(kind) {
  return KIND_LABELS[kind] || kind;
}

export function statusColor(status) {
  switch (status) {
    case 'Completed': return 'success';
    case 'Failed': return 'danger';
    default: return 'warning';
  }
}

export function currentLanguage() {
  return localStorage.getItem('language') || 'en';
}

/** "WordStatusChanged" reads better as "Word status changed". */
export function humanize(name) {
  const spaced = name.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
