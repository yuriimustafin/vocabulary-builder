// @ts-check
const { defineConfig, devices } = require('@playwright/test');
const { AUTH_FILE } = require('./tests/e2e/helpers/auth');

/**
 * @see https://playwright.dev/docs/test-configuration
 */
module.exports = defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // One worker, always. Every spec shares a single in-memory database and resets it in
  // beforeEach, so specs running side by side clear each other's data mid-test: a parallel
  // run fails a dozen tests, and a different dozen each time. CI already ran with one
  // worker; this makes a local run behave the same, at the cost of a few minutes.
  workers: 1,
  reporter: 'html',
  use: {
    baseURL: process.env.BASE_URL || 'https://localhost:44447',  // React dev server (proxies /api to backend)
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    ignoreHTTPSErrors: true,
  },

  projects: [
    // Signs in once and saves the session; see tests/e2e/auth.setup.js
    {
      name: 'setup',
      testMatch: /auth\.setup\.js/,
    },
    {
      name: 'chromium',
      // Every spec starts signed in as the administrator. The saved cookie reaches the
      // request fixture as well as the browser, so API calls from a spec are signed in too.
      use: { ...devices['Desktop Chrome'], storageState: AUTH_FILE },
      dependencies: ['setup'],
    },
  ],

  webServer: process.env.SKIP_WEBSERVER ? undefined : {
    command: 'dotnet run --project src/Web/Web.csproj --launch-profile E2E',
    url: 'https://localhost:44447',  // Wait for React dev server to be ready (backend starts it via SpaProxy)
    reuseExistingServer: !process.env.CI,
    timeout: 180 * 1000,  // 3 minutes for backend + webpack compilation
    ignoreHTTPSErrors: true,
  },
});
