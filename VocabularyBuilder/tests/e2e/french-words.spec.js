const { test, expect } = require('@playwright/test');
const { findWordByHeadword } = require('./helpers/api-helpers');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

/**
 * French words parsed from WordReference: articles chosen by gender and coloured by it,
 * and the conjugation shown on the word.
 *
 * Words come in through the real import, which in E2E mode reads the pages recorded under
 * src/Web/MockData/wordreference. Each fixture covers one way a noun can be tagged:
 * maison (nf), arbre (nm, vowel), hache (nf, aspirated h), livre (nm and nf meanings),
 * élève (either gender), gens (plural only), prendre (a verb, with its conjugation).
 */
test.describe('French words', () => {
  test.describe.configure({ mode: 'serial' });

  const MASCULINE = 'rgb(21, 101, 192)';
  const FEMININE = 'rgb(198, 40, 40)';
  const EITHER = 'rgb(106, 27, 154)';

  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);

    // The app reads the language from local storage on load
    await page.addInitScript(() => localStorage.setItem('language', 'fr'));
  });

  async function importFrench(request, words) {
    const response = await request.post('/api/NewWords/import?lang=fr&parseImmediately=true', {
      headers: { 'Content-Type': 'text/plain; charset=utf-8' },
      data: words.join('\n')
    });

    expect(response.ok(), await response.text()).toBeTruthy();
    const result = await response.json();
    expect(result.importedWords.sort()).toEqual([...words].sort());
    return result;
  }

  async function openWords(page) {
    await page.goto('/words');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 30000 });
  }

  function rowFor(page, headword) {
    return page.locator('table tbody tr').filter({
      has: page.locator('td', { hasText: new RegExp(`^(\\S+ ?)?${headword}$`) })
    });
  }

  async function openDetails(page, headword) {
    await rowFor(page, headword).getByRole('button', { name: 'View' }).click();
    await expect(page.getByTestId('details-headword')).toBeVisible({ timeout: 10000 });
  }

  test('the word list shows each noun with its article, coloured by gender', async ({ request, page }) => {
    await importFrench(request, ['maison', 'arbre', 'hache', 'élève', 'gens', 'prendre']);
    await openWords(page);

    const expected = [
      { headword: 'maison', shown: 'la maison', article: 'la', gender: 'feminine', colour: FEMININE },
      { headword: 'arbre', shown: "l'arbre", article: "l'", gender: 'masculine', colour: MASCULINE },
      // Aspirated h: no elision
      { headword: 'hache', shown: 'la hache', article: 'la', gender: 'feminine', colour: FEMININE },
      { headword: 'élève', shown: "l'élève", article: "l'", gender: 'common', colour: EITHER },
      // Plural only: "les" hides the gender, the colour still carries it
      { headword: 'gens', shown: 'les gens', article: 'les', gender: 'masculine', colour: MASCULINE }
    ];

    for (const { headword, shown, article, gender, colour } of expected) {
      const cell = rowFor(page, headword).locator('td').nth(1);
      await expect(cell).toHaveText(shown);

      const articleElement = cell.getByTestId('noun-article');
      await expect(articleElement).toHaveText(article);
      await expect(articleElement).toHaveAttribute('data-gender', gender);
      await expect(articleElement).toHaveCSS('color', colour);
    }

    // A verb has no article
    const verbCell = rowFor(page, 'prendre').locator('td').nth(1);
    await expect(verbCell).toHaveText('prendre');
    await expect(verbCell.getByTestId('noun-article')).toHaveCount(0);
  });

  test('a noun whose meanings differ in gender shows the article for each meaning', async ({ request, page }) => {
    await importFrench(request, ['livre']);
    await openWords(page);
    await openDetails(page, 'livre');

    await expect(page.getByTestId('details-headword')).toHaveText('le livre');

    // A book is "le livre", a pound is "la livre"
    const senses = page.getByTestId('sense-article');
    await expect(senses).toHaveText(['le livre', 'le livre', 'la livre', 'la livre', 'la livre']);
    await expect(senses.nth(0).getByTestId('noun-article')).toHaveCSS('color', MASCULINE);
    await expect(senses.nth(2).getByTestId('noun-article')).toHaveCSS('color', FEMININE);
  });

  test('an elided article is followed by the indefinite one, which shows the gender', async ({ request, page }) => {
    await importFrench(request, ['arbre', 'maison']);
    await openWords(page);

    await openDetails(page, 'arbre');
    await expect(page.getByTestId('details-headword')).toContainText("l'arbre");
    await expect(page.getByTestId('indefinite-article')).toHaveText('(un arbre)');
    await page.locator('.modal-header .btn-close').click();

    // "la" already says it, so no hint
    await openDetails(page, 'maison');
    await expect(page.getByTestId('details-headword')).toHaveText('la maison');
    await expect(page.getByTestId('indefinite-article')).toHaveCount(0);
  });

  test('a verb shows its conjugation as tables, and a noun shows none', async ({ request, page }) => {
    await importFrench(request, ['prendre', 'maison']);
    await openWords(page);
    await openDetails(page, 'prendre');

    const forms = page.getByTestId('word-forms');
    await expect(forms).toBeVisible();

    const moods = forms.getByTestId('word-forms-mood').locator('h6');
    await expect(moods).toHaveText([
      'participe',
      'indicatif',
      'formes composées / compound tenses',
      'subjonctif',
      'conditionnel',
      'impératif'
    ], { ignoreCase: true });

    // Participles come from their own block on the page
    await expect(forms).toContainText('prenant');

    // Every person is shown, including a form that repeats ("je prends", "tu prends")
    const indicative = forms.getByTestId('word-forms-mood').nth(1);
    const present = indicative.locator('.border').first();
    await expect(present.locator('tr')).toHaveText([
      /je\s*prends/,
      /tu\s*prends/,
      /il, elle, on\s*prend/,
      /nous\s*prenons/,
      /vous\s*prenez/,
      /ils, elles\s*prennent/
    ]);

    // The imperative has no dash placeholders and no exclamation marks
    const imperative = forms.getByTestId('word-forms-mood').last();
    await expect(imperative).not.toContainText('–');
    await expect(imperative).not.toContainText('!');

    await page.locator('.modal-header .btn-close').click();
    await openDetails(page, 'maison');
    await expect(page.getByTestId('word-forms')).toHaveCount(0);
  });

  test('editing a noun keeps its gender, and a gender can be set by hand', async ({ request, page }) => {
    await importFrench(request, ['gens']);
    await openWords(page);

    // Save the edit form without touching anything
    await rowFor(page, 'gens').getByRole('button', { name: 'Edit' }).click();
    await expect(page.locator('select#gender')).toHaveValue('1');
    await expect(page.locator('#isPluralOnly')).toBeChecked();
    await page.getByRole('button', { name: 'Update' }).click();
    await expect(page.locator('.modal.show')).toHaveCount(0);

    const gens = await findWordByHeadword(page, 'gens', 'fr');
    expect(gens.gender).toBe(1);
    expect(gens.isPluralOnly).toBe(true);
    await expect(rowFor(page, 'gens').locator('td').nth(1)).toHaveText('les gens');

    // A word typed in by hand has no dictionary data, so its gender is chosen in the form
    await page.getByRole('button', { name: 'Add New Word' }).click();
    await page.fill('input[name="headword"]', 'voiture');
    await page.selectOption('select#gender', '2');
    await page.getByRole('button', { name: 'Create' }).click();
    await expect(page.locator('.modal.show')).toHaveCount(0);

    await expect(rowFor(page, 'voiture').locator('td').nth(1)).toHaveText('la voiture', { timeout: 10000 });
  });

  test('a study card shows the article, but the prompt stays the bare word', async ({ request, page }) => {
    await importFrench(request, ['maison']);

    await page.goto('/study');
    await page.waitForSelector('#root', { timeout: 60000 });
    await expect(page.getByTestId('introduction-card')).toBeVisible({ timeout: 30000 });

    // Answers are marked against the headword alone, so the article sits beside it
    await expect(page.getByTestId('exercise-prompt')).toHaveText('maison');
    const article = page.getByTestId('introduction-card').getByTestId('noun-article');
    await expect(article).toHaveText('la');
    await expect(article).toHaveCSS('color', FEMININE);
    await expect(page.getByTestId('introduction-card').locator('.display-6')).toHaveText('la maison');
  });

  test('the Anki export puts French words in the French deck, with the article in the cloze', async ({ request, page }) => {
    await importFrench(request, ['maison', 'arbre', 'prendre']);

    const ids = [];
    for (const headword of ['maison', 'arbre', 'prendre']) {
      ids.push((await findWordByHeadword(page, headword, 'fr')).id);
    }

    const response = await request.post('/api/fr/words/export', { data: { wordIds: ids } });
    expect(response.ok()).toBeTruthy();
    const lines = (await response.text()).split('\r\n').filter(line => line.length > 0);

    expect(lines).toHaveLength(3);
    for (const line of lines) {
      expect(line.startsWith('French::Vocabulary;')).toBeTruthy();
    }

    const line = headword => lines.find(l => l.split(';')[1] === headword);
    expect(line('maison')).toContain(
      "{{c1::<span class='article article-feminine' style='color:#c62828'>la</span> maison}}");
    expect(line('arbre')).toContain(
      "{{c1::<span class='article article-masculine' style='color:#1565c0'>l'</span>arbre}}");
    // A French verb gets neither an article nor the English "to"
    expect(line('prendre')).toContain('{{c1::prendre}}');
  });
});
