/**
 * Database fixtures for E2E tests
 * Provides setup and teardown functions for test isolation
 */

const { resetDatabase } = require('./api-helpers');

/**
 * Set up a clean database before each test
 * This fixture can be used in beforeEach hooks
 * @param {import('@playwright/test').Page | import('@playwright/test').APIRequestContext} pageOrRequest - Playwright page or request context
 * @param {Object} options - Options
 * @param {boolean} [options.skipCleanup=false] - Skip cleanup (useful for debugging)
 */
async function setupCleanDatabase(pageOrRequest, options = {}) {
  const { skipCleanup = false } = options;
  
  if (skipCleanup) {
    console.log('Skipping database cleanup (skipCleanup=true)');
    return;
  }
  
  try {
    // Reset the entire in-memory database (much faster than deleting words one-by-one)
    await resetDatabase(pageOrRequest);
  } catch (error) {
    console.error('Failed to reset database:', error);
    // Don't throw - let tests proceed even if cleanup fails
    // This is important for parallel test execution
  }
}

/**
 * Global fixture to be used in test configuration
 * Returns an object with beforeEach and afterEach handlers
 */
function createDatabaseFixture(options = {}) {
  return {
    async beforeEach(page) {
      await setupCleanDatabase(page, options);
    },
    
    async afterEach(page) {
      // Optional: cleanup after each test as well
      // Usually not needed since beforeEach handles it
    }
  };
}

module.exports = {
  setupCleanDatabase,
  createDatabaseFixture
};
