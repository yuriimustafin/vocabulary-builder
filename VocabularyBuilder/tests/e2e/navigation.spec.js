const { test, expect } = require('@playwright/test');

test.describe('Navigation and Home Page', () => {
  // Configure retries for stability
  test.describe.configure({ retries: 2 });

  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    
    // Wait for React app to load
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
  });

  test('should load home page successfully', async ({ page }) => {
    // Verify we're on the home page
    expect(page.url()).toContain('/');
    
    // Check for main navigation or header
    await expect(page.locator('nav, header, .navbar')).toBeVisible();
  });

  test('should display navigation menu', async ({ page }) => {
    // Check for key navigation links
    const navLinks = [
      'Words',
      'Bulk Import',
      'Kindle Import',
      'Export'
    ];
    
    for (const linkText of navLinks) {
      const link = page.locator(`nav a:has-text("${linkText}"), .navbar a:has-text("${linkText}")`);
      expect(await link.count()).toBeGreaterThanOrEqual(1);
    }
  });

  test('should navigate to Words page', async ({ page }) => {
    await page.click('a:has-text("Words")');
    
    // Wait for React app to load on new page
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    expect(page.url()).toContain('/words');
    await expect(page.locator('h1, h2, h3')).toContainText(/Words/i, { timeout: 10000 });
  });

  test('should navigate to Bulk Import page', async ({ page }) => {
    await page.click('a:has-text("Bulk Import")');
    
    // Wait for React app to load on new page
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    expect(page.url()).toContain('/bulk-import');
    await expect(page.locator('h1, h2, h3, h4')).toContainText(/Import/i, { timeout: 10000 });
  });

  test('should navigate to Kindle Import page', async ({ page }) => {
    await page.click('a:has-text("Kindle Import")');
    
    // Wait for React app to load on new page
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    expect(page.url()).toContain('/kindle-import');
    await expect(page.locator('h1, h2, h3, h4')).toContainText(/Kindle|Import/i, { timeout: 10000 });
  });

  test('should navigate to Export page', async ({ page }) => {
    await page.click('a:has-text("Export")');
    
    // Wait for React app to load on new page
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    expect(page.url()).toContain('/export');
    await expect(page.locator('h1, h2, h3, h4')).toContainText(/Export/i, { timeout: 10000 });
  });

  test('should handle browser back/forward navigation', async ({ page }) => {
    // Navigate to Words page
    await page.click('a:has-text("Words")');
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/words');
    
    // Navigate to Import page
    await page.click('a:has-text("Bulk Import")');
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/bulk-import');
    
    // Go back
    await page.goBack();
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/words');
    
    // Go forward
    await page.goForward();
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/bulk-import');
  });

  test('should persist language selection across navigation', async ({ page }) => {
    // Set language to French
    await page.evaluate(() => {
      localStorage.setItem('language', 'fr');
    });
    
    // Reload to apply language
    await page.reload();
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    // Navigate to another page
    await page.click('a:has-text("Words")');
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    // Check that language persisted
    const language = await page.evaluate(() => localStorage.getItem('language'));
    expect(language).toBe('fr');
    
    // Reset to English
    await page.evaluate(() => {
      localStorage.setItem('language', 'en');
    });
  });

  test('should have responsive navigation on mobile', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(500);
    
    // Check if mobile menu toggle exists (hamburger menu)
    const mobileToggle = page.locator('.navbar-toggler, button[aria-label="Toggle navigation"]');
    
    if (await mobileToggle.isVisible()) {
      // Click to open mobile menu
      await mobileToggle.click();
      await page.waitForTimeout(300);
      
      // Verify menu items are now visible
      await expect(page.locator('a:has-text("Words")')).toBeVisible();
    }
  });

  test('should display app title/logo', async ({ page }) => {
    // Check for app title or logo
    const title = page.locator('.navbar-brand, h1, .logo, text=/Vocabulary Builder/i');
    await expect(title.first()).toBeVisible();
  });

  test('should handle language switcher if available', async ({ page }) => {
    // Look for language selector
    const langSelector = page.locator('select#language, button:has-text("EN"), button:has-text("FR")');
    
    if (await langSelector.count() > 0) {
      const firstSelector = langSelector.first();
      await expect(firstSelector).toBeVisible();
      
      // Try to interact with it
      if (await firstSelector.evaluate(el => el.tagName === 'SELECT')) {
        // It's a select dropdown
        await firstSelector.selectOption({ index: 0 });
      } else {
        // It's a button or link
        await firstSelector.click();
      }
    }
  });

  test('should load without console errors', async ({ page }) => {
    const consoleErrors = [];
    
    page.on('console', msg => {
      if (msg.type() === 'error') {
        consoleErrors.push(msg.text());
      }
    });
    
    await page.goto('/');
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
    
    // Filter out known acceptable errors (e.g., dev server, known warnings)
    const criticalErrors = consoleErrors.filter(error => {
      return !error.includes('DevTools') && 
             !error.includes('source map') &&
             !error.includes('favicon');
    });
    
    expect(criticalErrors.length).toBe(0);
  });
});
