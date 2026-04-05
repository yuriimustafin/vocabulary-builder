const { test: base } = require('@playwright/test');

/**
 * Custom test fixtures for E2E tests
 */
exports.test = base.extend({
  /**
   * Helper to create a test word
   */
  createTestWord: async ({ page }, use) => {
    const words = [];
    
    const createWord = async (wordData = {}) => {
      const timestamp = Date.now();
      const word = {
        headword: wordData.headword || `testword${timestamp}_${Math.random().toString(36).substr(2, 5)}`,
        transcription: wordData.transcription || '/test/',
        partOfSpeech: wordData.partOfSpeech || 'noun',
        examples: wordData.examples || 'Test example sentence.',
        ...wordData
      };
      
      // Navigate to words page
      await page.goto('/words');
      await page.waitForLoadState('networkidle');
      
      // Click Add Word
      await page.click('button:has-text("Add Word")');
      
      // Fill form
      await page.fill('input[name="headword"]', word.headword);
      await page.fill('input[name="transcription"]', word.transcription);
      await page.fill('input[name="partOfSpeech"]', word.partOfSpeech);
      await page.fill('textarea[name="examples"]', word.examples);
      
      // Save
      await page.click('button:has-text("Save")');
      await page.waitForTimeout(500);
      
      words.push(word);
      return word;
    };
    
    await use(createWord);
    
    // Cleanup: delete created words
    // (Optional - depends on whether you want to keep test data)
  },
  
  /**
   * Helper to set language
   */
  setLanguage: async ({ page }, use) => {
    const setLang = async (lang) => {
      await page.evaluate((language) => {
        localStorage.setItem('language', language);
      }, lang);
    };
    
    await use(setLang);
  },
  
  /**
   * Helper to wait for API calls
   */
  waitForApiResponse: async ({ page }, use) => {
    const waitFor = async (urlPattern, options = {}) => {
      return await page.waitForResponse(
        response => response.url().includes(urlPattern) && response.status() === 200,
        { timeout: 30000, ...options }
      );
    };
    
    await use(waitFor);
  }
});

exports.expect = require('@playwright/test').expect;
