/**
 * API helper functions for E2E tests
 * These functions interact directly with the backend API to verify database state
 */

/**
 * Fetch words from the database via API
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @param {Object} options - Query options
 * @param {string} [options.lang='en'] - Language
 * @param {string} [options.searchTerm] - Search term to filter by headword
 * @param {number} [options.pageSize=100] - Number of results per page
 * @returns {Promise<Array>} Array of word objects
 */
async function getWordsFromDb(page, options = {}) {
  const { lang = 'en', searchTerm, pageSize = 100 } = options;
  
  // Build query parameters (relative URL uses baseURL from playwright.config.js)
  let url = `/api/${lang}/words?pageSize=${pageSize}`;
  
  const response = await page.request.get(url);
  
  if (!response.ok()) {
    throw new Error(`Failed to fetch words: ${response.status()} ${response.statusText()}`);
  }
  
  const data = await response.json();
  
  // Filter by search term if provided
  let words = data.items || [];
  if (searchTerm) {
    words = words.filter(w => 
      w.headword && w.headword.toLowerCase().includes(searchTerm.toLowerCase())
    );
  }
  
  return words;
}

/**
 * Find a specific word by headword
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @param {string} headword - The word to search for
 * @param {string} [lang='en'] - Language
 * @returns {Promise<Object|null>} Word object or null if not found
 */
async function findWordByHeadword(page, headword, lang = 'en') {
  const words = await getWordsFromDb(page, { lang, pageSize: 1000 });
  return words.find(w => w.headword === headword) || null;
}

/**
 * Wait for a word to appear in the database
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @param {string} headword - The word to wait for
 * @param {Object} options - Options
 * @param {string} [options.lang='en'] - Language
 * @param {number} [options.timeout=10000] - Timeout in milliseconds
 * @returns {Promise<Object>} Word object
 */
async function waitForWordInDb(page, headword, options = {}) {
  const { lang = 'en', timeout = 10000 } = options;
  const startTime = Date.now();
  
  while (Date.now() - startTime < timeout) {
    const word = await findWordByHeadword(page, headword, lang);
    if (word) {
      return word;
    }
    // Wait 200ms before retrying
    await page.waitForTimeout(200);
  }
  
  throw new Error(`Word "${headword}" not found in database after ${timeout}ms`);
}

/**
 * Clear all words from the database (destructive!)
 * This calls the delete endpoint for all words
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @param {string} [lang='en'] - Language
 */
async function clearAllWords(page, lang = 'en') {
  const words = await getWordsFromDb(page, { lang, pageSize: 10000 });
  
  // Delete words in batches to avoid overwhelming the API
  for (const word of words) {
    await page.request.delete(`/api/${lang}/words/${word.id}`);
  }
  
  // Verify all words are deleted
  const remainingWords = await getWordsFromDb(page, { lang, pageSize: 1 });
  if (remainingWords.length > 0) {
    console.warn(`Warning: ${remainingWords.length} words still remain after clearAllWords`);
  }
}

/**
 * Reset the entire database (E2E Test environment only)
 * Drops and recreates the in-memory database with fresh seed data
 * This is much faster than deleting words one by one
 * @param {import('@playwright/test').Page | import('@playwright/test').APIRequestContext} pageOrRequest - Playwright page or request context
 * @returns {Promise<void>}
 */
async function resetDatabase(pageOrRequest) {
  // Support both Page and APIRequestContext
  const requestContext = pageOrRequest.request || pageOrRequest;
  const response = await requestContext.post('/api/e2e-testing/reset-database');
  
  if (!response.ok()) {
    // Try to get error details from response
    let errorDetail = '';
    try {
      const errorData = await response.json();
      errorDetail = JSON.stringify(errorData, null, 2);
    } catch {
      errorDetail = await response.text();
    }
    
    throw new Error(
      `Failed to reset database: ${response.status()} ${response.statusText()}\n` +
      `Details: ${errorDetail}`
    );
  }
  
  const data = await response.json();
  return data;
}

/**
 * Create a unique test word identifier with timestamp
 * @param {string} baseWord - Base word to make unique
 * @returns {string} Unique word identifier
 */
function createUniqueWord(baseWord) {
  const timestamp = Date.now();
  const random = Math.floor(Math.random() * 1000);
  return `${baseWord}_${timestamp}_${random}`;
}

/**
 * Verify that words with specific pattern exist in database
 * @param {import('@playwright/test').Page} page - Playwright page object
 * @param {string|RegExp} pattern - Pattern to match headwords against
 * @param {Object} options - Options
 * @param {string} [options.lang='en'] - Language
 * @param {number} [options.expectedCount] - Expected number of matches
 * @param {number} [options.minCount] - Minimum number of matches
 * @returns {Promise<Array>} Array of matching words
 */
async function verifyWordsExist(page, pattern, options = {}) {
  const { lang = 'en', expectedCount, minCount } = options;
  
  const words = await getWordsFromDb(page, { lang, pageSize: 10000 });
  
  const matches = words.filter(w => {
    if (pattern instanceof RegExp) {
      return pattern.test(w.headword);
    }
    return w.headword.includes(pattern);
  });
  
  if (expectedCount !== undefined && matches.length !== expectedCount) {
    throw new Error(
      `Expected ${expectedCount} words matching "${pattern}", but found ${matches.length}`
    );
  }
  
  if (minCount !== undefined && matches.length < minCount) {
    throw new Error(
      `Expected at least ${minCount} words matching "${pattern}", but found ${matches.length}`
    );
  }
  
  return matches;
}

module.exports = {
  getWordsFromDb,
  findWordByHeadword,
  waitForWordInDb,
  clearAllWords,
  resetDatabase,
  createUniqueWord,
  verifyWordsExist
};
