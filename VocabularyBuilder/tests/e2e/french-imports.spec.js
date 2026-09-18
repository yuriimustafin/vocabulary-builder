const path = require('path');
const { test, expect } = require('@playwright/test');
const { getWordsFromDb } = require('./helpers/api-helpers');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

const HEADER =
  'term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2';

/**
 * The frequency data is what lets a conjugation reach its infinitive without a model, and
 * resetting the database clears it, so every test seeds it again. The sample mirrors the
 * bundled Lexique file, including the part that matters most: "est" and "a" have lemma
 * lines of their own, so they cannot be reduced and fall through to the model.
 */
async function seedFrequencyData(request) {
  const filePath = path.resolve(__dirname, 'fixtures', 'frequency-words-fr-sample.txt');

  const response = await request.post(
    `/api/NewWords/import-frequency?lang=fr&filePath=${encodeURIComponent(filePath)}`
  );

  expect(response.ok()).toBeTruthy();
}

async function uploadExport(page, rows, { importName } = {}) {
  if (importName) {
    await page.fill('input[name="listName"]', importName);
  }

  await page.setInputFiles('#lingqFileInput', {
    name: 'lingqs.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(HEADER + '\n' + rows, 'utf-8')
  });

  await page.click('button:has-text("Import Vocabulary")');
  await expect(page.locator('.alert-success')).toBeVisible({ timeout: 60000 });
}

async function frenchWords(page) {
  return getWordsFromDb(page, { lang: 'fr', pageSize: 1000 });
}

async function encountersFor(page, headword) {
  const words = await frenchWords(page);
  const word = words.find(w => w.headword === headword);
  return word ? word.encounterCount : null;
}

test.describe('French imports', () => {
  // One in-memory database is shared by the whole run and each test resets it, so these
  // have to take turns - in parallel they clear each other's words mid-test
  test.describe.configure({ mode: 'serial' });

  test.beforeEach(async ({ request, page }) => {
    await setupCleanDatabase(request);
    await seedFrequencyData(request);

    // Both imports read the language the user picked in the nav
    await page.addInitScript(() => window.localStorage.setItem('language', 'fr'));
  });

  test.describe('LingQ', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/lingq-import');
      await page.waitForSelector('#root', { timeout: 60000 });
      await page.waitForLoadState('networkidle');
    });

    test('should display the LingQ import page', async ({ page }) => {
      await expect(page.locator('h1')).toContainText(/Import from LingQ/i);
      await expect(page.locator('#lingqFileInput')).toBeVisible();
      await expect(page.locator('button:has-text("Import Vocabulary")')).toBeVisible();
    });

    test('should store the headword without its article', async ({ page }) => {
      await uploadExport(page, 'une conférence,,preply,,,,en,conference,,\n');

      const words = await frenchWords(page);

      expect(words.map(w => w.headword)).toContain('conférence');
      expect(words.map(w => w.headword)).not.toContain('une conférence');
    });

    test('should count two articles of one noun as one word met twice', async ({ page }) => {
      await uploadExport(
        page,
        'une randonnée,,preply,,,,en,a hike,,\nla randonnée,,preply,,,,en,hiking,,\n'
      );

      const words = await frenchWords(page);
      const randonnee = words.filter(w => w.headword === 'randonnée');

      expect(randonnee).toHaveLength(1);
      expect(randonnee[0].encounterCount).toBe(2);
    });

    /**
     * The frequency-data tier: every person of the verb reaches the infinitive with no
     * model involved, which is what turns a conjugation table into one word met six times.
     */
    test('should trace a conjugation back to its infinitive', async ({ page }) => {
      await uploadExport(
        page,
        'je finis,,preply,,,,en,I finish,,\n' +
          'tu finis,,preply,,,,en,you finish,,\n' +
          'nous finissons,,preply,,,,en,we finish,,\n' +
          'vous finissez,,preply,,,,en,you finish,,\n'
      );

      const words = await frenchWords(page);
      const finir = words.filter(w => w.headword === 'finir');

      expect(finir).toHaveLength(1);
      // "je finis" and "tu finis" are the same written form, so they share an encounter
      expect(finir[0].encounterCount).toBeGreaterThanOrEqual(3);

      expect(words.map(w => w.headword)).not.toContain('finissons');
    });

    test('should bring two different verbs to their own infinitives', async ({ page }) => {
      await uploadExport(
        page,
        'nous allons,,preply,,,,en,we go,,\nnous chantons,,preply,,,,en,we sing,,\n'
      );

      const headwords = (await frenchWords(page)).map(w => w.headword);

      expect(headwords).toContain('aller');
      expect(headwords).toContain('chanter');
    });

    test('should report sentences and questions rather than importing them', async ({ page }) => {
      await uploadExport(
        page,
        'un oiseau,,preply,,,,en,a bird,,\n' +
          'Quelle heure est-il,,preply,,,,en,What time is it?,,\n' +
          'il fait beau,,preply,,,,en,the weather is fine,,\n' +
          'je parle ukrainien et anglais,,,,,,en,I speak Ukrainian,,\n'
      );

      const warning = page.locator('.alert-warning');
      await expect(warning).toBeVisible();
      await expect(warning).toContainText('Quelle heure est-il');
      await expect(warning).toContainText('il fait beau');

      const headwords = (await frenchWords(page)).map(w => w.headword);
      expect(headwords).toContain('oiseau');
      expect(headwords).not.toContain('il fait beau');
    });

    /**
     * Re-importing an export must not inflate the counts, or every import would make every
     * word look better known than it is.
     */
    test('should add nothing when the same export is imported again', async ({ page }) => {
      const rows = 'un oiseau,,preply,,,,en,a bird,,\nune conférence,,preply,,,,en,conference,,\n';

      await uploadExport(page, rows, { importName: 'Preply' });
      const first = await encountersFor(page, 'oiseau');

      await page.reload();
      await page.waitForSelector('#root', { timeout: 60000 });
      await uploadExport(page, rows, { importName: 'Preply' });

      expect(await encountersFor(page, 'oiseau')).toBe(first);
      expect((await frenchWords(page)).filter(w => w.headword === 'oiseau')).toHaveLength(1);
    });

    /**
     * An imported word is filled in from the dictionary later, so nothing arrives carrying
     * the learner's own translation.
     */
    test('should not take the words content from the export', async ({ page }) => {
      await uploadExport(page, 'un oiseau,,preply,,,,en,a bird,,\n');

      const oiseau = (await frenchWords(page)).find(w => w.headword === 'oiseau');

      expect(oiseau).toBeTruthy();
      expect(oiseau.senseCount || 0).toBe(0);
    });
  });

  test.describe('Lesson notes', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/notes-import');
      await page.waitForSelector('#root', { timeout: 60000 });
      await page.waitForLoadState('networkidle');
    });

    test('should display the notes import page', async ({ page }) => {
      await expect(page.locator('h1')).toContainText(/Import from Lesson Notes/i);
      await expect(page.locator('textarea[name="notes"]')).toBeVisible();
    });

    test('should read the vocabulary out of pasted notes and drop the translations', async ({ page }) => {
      await page.fill(
        'textarea[name="notes"]',
        'un serpent=snake\nmigrer=to migrate\nune conférence=conference'
      );

      await page.click('button:has-text("Import Vocabulary")');
      await expect(page.locator('.alert-success')).toBeVisible({ timeout: 60000 });

      const headwords = (await frenchWords(page)).map(w => w.headword);

      expect(headwords).toContain('serpent');
      expect(headwords).toContain('migrer');
      expect(headwords).toContain('conférence');

      // The English side of each line is a translation, not a word to learn
      expect(headwords).not.toContain('snake');
      expect(headwords).not.toContain('to migrate');
    });

    test('should report prose in the notes rather than importing it', async ({ page }) => {
      await page.fill(
        'textarea[name="notes"]',
        'un serpent=snake\nQuelle heure est-il\nil fait beau'
      );

      await page.click('button:has-text("Import Vocabulary")');
      await expect(page.locator('.alert-success')).toBeVisible({ timeout: 60000 });

      const warning = page.locator('.alert-warning');
      await expect(warning).toBeVisible();
      await expect(warning).toContainText('Quelle heure est-il');

      expect((await frenchWords(page)).map(w => w.headword)).toContain('serpent');
    });

    test('should clear the notes after a successful import', async ({ page }) => {
      await page.fill('textarea[name="notes"]', 'un serpent=snake');
      await page.click('button:has-text("Import Vocabulary")');

      await expect(page.locator('.alert-success')).toBeVisible({ timeout: 60000 });
      await expect(page.locator('textarea[name="notes"]')).toHaveValue('');
    });
  });
});
