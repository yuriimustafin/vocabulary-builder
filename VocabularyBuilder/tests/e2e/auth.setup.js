// @ts-check
const { test: setup, expect } = require('@playwright/test');
const { ADMIN, AUTH_FILE } = require('./helpers/auth');

/**
 * Signs in once, before any spec runs, and saves the session for all of them.
 *
 * Every API call needs a signed-in user, and each spec's data belongs to whoever created it,
 * so the whole suite runs as one account: the administrator, because the French import
 * specs load frequency data and that is an administrator's job. Saving the cookie means no
 * spec has to know about signing in, and none of them pays for a login of its own.
 */
setup('sign in as the end-to-end administrator', async ({ request }) => {
  const response = await request.post('/api/Users/login?useCookies=true', {
    data: { email: ADMIN.email, password: ADMIN.password }
  });

  expect(response.ok(), `login failed with ${response.status()}: ${await response.text()}`).toBeTruthy();

  await request.storageState({ path: AUTH_FILE });
});

/**
 * Loads the app once, in a browser, before any spec does.
 *
 * `webServer.url` waits for the React dev server to answer, which it does as soon as it is
 * listening - but the first navigation from a real browser is what makes webpack compile and
 * serve the bundle, and on a cold start that takes long enough to outlast an ordinary
 * assertion timeout. Whichever spec ran first paid for it, and paid intermittently: the
 * suite would fail one test on a cold run and pass it on the next. The login above is an API
 * call and never touches the bundle, so nothing warmed it.
 *
 * Generous timeouts on purpose - this step is allowed to be slow, so that no spec has to be.
 */
setup('warm the dev server so the first spec does not pay for it', async ({ page }) => {
  await page.goto('/', { timeout: 120000 });
  await page.waitForSelector('#root', { timeout: 120000 });
});
