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
