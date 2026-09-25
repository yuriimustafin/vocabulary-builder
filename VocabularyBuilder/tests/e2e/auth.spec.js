// @ts-check
const { test, expect } = require('@playwright/test');
const { resetDatabase } = require('./helpers/api-helpers');
const { ADMIN, LEARNER, AUTH_FILE, SIGNED_OUT } = require('./helpers/auth');

/**
 * Signing up, in and out, and what one account can see of another's.
 *
 * Unlike every other spec these start signed out. The administrator's saved session still
 * does the resetting and the seeding, through a request context of its own, so the browser
 * and the default request fixture are the only things here without it.
 */
test.describe('Accounts', () => {
  test.use({ storageState: SIGNED_OUT });

  /** @type {import('@playwright/test').APIRequestContext} */
  let admin;

  test.beforeEach(async ({ playwright }) => {
    admin = await playwright.request.newContext({ storageState: AUTH_FILE });

    // Called directly rather than through setupCleanDatabase, which carries on past a
    // failed reset - and a reset refused for want of a session is exactly what could fail here
    await resetDatabase(admin);
  });

  test.afterEach(async () => {
    await admin.dispose();
  });

  async function signIn(page, { email, password }) {
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign in' }).click();
  }

  test('the API refuses anyone not signed in', async ({ request }) => {
    const response = await request.get('/api/en/words');

    expect(response.status()).toBe(401);
  });

  test('a page asked for while signed out leads to the login form, and back to the page after', async ({ page }) => {
    await page.goto('/words');

    await expect(page).toHaveURL(/\/login\?returnUrl=%2Fwords$/);
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();

    await signIn(page, ADMIN);

    await expect(page).toHaveURL(/\/words$/);
    await expect(page.getByTestId('user-menu')).toHaveText(ADMIN.email);
  });

  test('a wrong password is turned away with a reason', async ({ page }) => {
    await page.goto('/login');

    await signIn(page, { email: ADMIN.email, password: 'not-the-password' });

    await expect(page.getByRole('alert')).toContainText('do not match');
    await expect(page).toHaveURL(/\/login/);
  });

  test('an email that was not invited cannot register', async ({ page }) => {
    await page.goto('/register');

    await page.getByLabel('Email').fill('stranger@example.com');
    await page.getByLabel('Password', { exact: true }).fill(LEARNER.password);
    await page.getByLabel('Confirm password').fill(LEARNER.password);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toContainText('not allowed to register');
  });

  test('an invited learner registers, and sees none of anyone else\'s words', async ({ page }) => {
    const seeded = await admin.post('/api/en/words', {
      data: { headword: 'adminsword', partOfSpeech: 'noun', frequency: 100 }
    });
    expect(seeded.ok()).toBeTruthy();

    await page.goto('/register');

    await page.getByLabel('Email').fill(LEARNER.email);
    await page.getByLabel('Password', { exact: true }).fill(LEARNER.password);
    await page.getByLabel('Confirm password').fill(LEARNER.password);
    await page.getByRole('button', { name: 'Create account' }).click();

    // Registering signs straight in
    await expect(page.getByTestId('user-menu')).toHaveText(LEARNER.email);

    await page.goto('/words');
    await expect(page.getByText('No words found')).toBeVisible();
    await expect(page.getByText('adminsword')).toHaveCount(0);

    // And the other way round: what the learner adds stays theirs
    const added = await page.request.post('/api/en/words', {
      data: { headword: 'learnersword', partOfSpeech: 'noun', frequency: 100 }
    });
    expect(added.ok()).toBeTruthy();

    const adminWords = await (await admin.get('/api/en/words?pageSize=100')).json();
    expect(adminWords.items.map(w => w.headword)).toEqual(['adminsword']);
  });

  test('signing out ends the session', async ({ page }) => {
    await page.goto('/login');
    await signIn(page, ADMIN);
    await expect(page.getByTestId('user-menu')).toBeVisible();

    await page.getByTestId('user-menu').click();
    await page.getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    expect((await page.request.get('/api/Users/me')).status()).toBe(401);
  });
});
