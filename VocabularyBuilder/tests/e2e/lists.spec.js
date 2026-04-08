const { test, expect } = require('@playwright/test');
const { setupCleanDatabase } = require('./helpers/db-fixtures');

test.describe('Lists Management', () => {
  // Configure retries for backend race conditions during parallel execution
  test.describe.configure({ retries: 2 });

  test.beforeEach(async ({ request, page }) => {
    // Clean database before each test for isolation
    await setupCleanDatabase(request);
    
    // Capture console messages for debugging
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`Browser console error: ${msg.text()}`);
      }
    });
    
    await page.goto('/lists');
    
    // Wait for React app to load
    await page.waitForSelector('#root', { timeout: 60000 });
    await page.waitForLoadState('networkidle');
  });

  /**
   * Helper to create a unique list title
   */
  function createUniqueListTitle(prefix = 'testlist') {
    return `${prefix}_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`;
  }

  /**
   * Helper to create a list via API
   */
  async function createListViaAPI(page, title, status = 0) {
    const language = localStorage.getItem('language') || 'en';
    const response = await page.request.post(`/api/${language}/lists`, {
      data: {
        title: title,
        status: status
      }
    });
    expect(response.ok()).toBeTruthy();
    const listId = await response.json();
    return listId;
  }

  /**
   * Helper to get list from API
   */
  async function getListFromAPI(page, listId) {
    const language = localStorage.getItem('language') || 'en';
    const response = await page.request.get(`/api/${language}/lists/${listId}`);
    if (!response.ok()) {
      return null;
    }
    return await response.json();
  }

  test('should display lists page with key UI elements', async ({ page }) => {
    // Verify page title
    await expect(page.locator('h1')).toContainText('Vocabulary Lists', { timeout: 10000 });
    
    // Check for key UI elements
    await expect(page.locator('table')).toBeVisible();
    await expect(page.locator('button:has-text("Add New List")')).toBeVisible();
    
    // Verify table headers
    await expect(page.locator('th:has-text("Title")')).toBeVisible();
    await expect(page.locator('th:has-text("Status")')).toBeVisible();
    await expect(page.locator('th:has-text("Items")')).toBeVisible();
  });

  test('should create a new list', async ({ page }) => {
    // Click Add New List button
    await page.click('button:has-text("Add New List")');
    
    // Wait for modal
    await expect(page.locator('.modal-title')).toContainText('Add New List', { timeout: 10000 });
    
    // Fill in list details with unique identifier
    const uniqueTitle = createUniqueListTitle('IELTS Phrases');
    await page.fill('input[name="title"]', uniqueTitle);
    await page.selectOption('select[name="status"]', '0'); // Active
    
    // Create the list
    await page.click('.modal-footer button:has-text("Create")');
    
    // Wait for modal to close
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait a bit longer for the API call and reload to complete
    await page.waitForTimeout(1500);
    
    // Verify the list appears in the UI
    const listCell = page.locator(`td:has-text("${uniqueTitle}")`);
    await expect(listCell).toBeVisible({ timeout: 10000 });
    
    // Verify status badge
    const statusBadge = page.locator(`tr:has-text("${uniqueTitle}") .badge`);
    await expect(statusBadge).toContainText('Active');
  });

  test('should update a list', async ({ page }) => {
    // First create a test list
    const originalTitle = createUniqueListTitle('Original List');
    await page.click('button:has-text("Add New List")');
    await expect(page.locator('.modal-title')).toContainText('Add New List', { timeout: 10000 });
    await page.fill('input[name="title"]', originalTitle);
    await page.click('.modal-footer button:has-text("Create")');
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait for list to appear
    await page.waitForTimeout(500);
    await expect(page.locator(`td:has-text("${originalTitle}")`)).toBeVisible();
    
    // Click Edit button
    const editButton = page.locator(`tr:has-text("${originalTitle}") button:has-text("Edit")`);
    await editButton.click();
    
    // Wait for edit modal
    await expect(page.locator('.modal-title')).toContainText('Edit List', { timeout: 10000 });
    
    // Modify the title and status
    const updatedTitle = createUniqueListTitle('Updated List');
    await page.fill('input[name="title"]', updatedTitle);
    await page.selectOption('select[name="status"]', '1'); // Completed
    
    // Save changes
    await page.click('.modal-footer button:has-text("Update")');
    
    // Wait for modal to close
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait for update to complete
    await page.waitForTimeout(500);
    
    // Verify the updated title appears
    await expect(page.locator(`td:has-text("${updatedTitle}")`)).toBeVisible();
    
    // Verify original title is gone
    await expect(page.locator(`td:has-text("${originalTitle}")`)).not.toBeVisible();
    
    // Verify status was updated
    const statusBadge = page.locator(`tr:has-text("${updatedTitle}") .badge`);
    await expect(statusBadge).toContainText('Completed');
  });

  test('should delete a list', async ({ page }) => {
    // Create a test list to delete
    const listTitle = createUniqueListTitle('Delete Me');
    await page.click('button:has-text("Add New List")');
    await expect(page.locator('.modal-title')).toContainText('Add New List', { timeout: 10000 });
    await page.fill('input[name="title"]', listTitle);
    await page.click('.modal-footer button:has-text("Create")');
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait for list to appear
    await page.waitForTimeout(500);
    await expect(page.locator(`td:has-text("${listTitle}")`)).toBeVisible();
    
    // Click delete button
    const deleteButton = page.locator(`tr:has-text("${listTitle}") button:has-text("Delete")`);
    await deleteButton.click();
    
    // Wait for confirmation modal
    await expect(page.locator('.modal-title')).toContainText('Confirm Delete', { timeout: 10000 });
    await expect(page.locator('.modal-body')).toContainText(listTitle);
    
    // Confirm deletion
    await page.click('.modal-footer button:has-text("Confirm")');
    
    // Wait for modal to close
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait for deletion to complete
    await page.waitForTimeout(500);
    
    // Verify the list is no longer visible
    await expect(page.locator(`td:has-text("${listTitle}")`)).not.toBeVisible();
  });

  test('should add, view, and manage items in a list', async ({ page }) => {
    // Create a test list
    const listTitle = createUniqueListTitle('Synonyms for Good');
    await page.click('button:has-text("Add New List")');
    await expect(page.locator('.modal-title')).toContainText('Add New List', { timeout: 10000 });
    await page.fill('input[name="title"]', listTitle);
    await page.click('.modal-footer button:has-text("Create")');
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Wait for list to appear
    await page.waitForTimeout(500);
    
    // Click View button to see list details
    const viewButton = page.locator(`tr:has-text("${listTitle}") button:has-text("View")`);
    await viewButton.click();
    
    // Wait for details modal
    await expect(page.locator('.modal-title')).toContainText(listTitle, { timeout: 10000 });
    
    // Verify initially no items
    await expect(page.locator('.modal-body')).toContainText('No items in this list yet');
    
    // Add first item
    await page.click('button:has-text("Add Item")');
    // Use .last() to target the top-most modal when multiple modals are open
    await expect(page.locator('.modal').last().locator('.modal-title')).toContainText('Add Item', { timeout: 10000 });
    await page.fill('input[name="text"]', 'excellent');
    // Click the Add button within the modal footer to avoid ambiguity
    await page.locator('.modal').last().locator('.modal-footer button:has-text("Add")').click();
    
    // Wait for item modal to close
    await page.waitForTimeout(500);
    
    // Add second item
    await page.click('button:has-text("Add Item")');
    await expect(page.locator('.modal').last().locator('.modal-title')).toContainText('Add Item', { timeout: 10000 });
    await page.fill('input[name="text"]', 'superb');
    await page.locator('.modal').last().locator('.modal-footer button:has-text("Add")').click();    
    // Wait for item modal to close
    await page.waitForTimeout(500);
    
    // Verify items appear in the list
    await expect(page.locator('td:has-text("excellent")')).toBeVisible();
    await expect(page.locator('td:has-text("superb")')).toBeVisible();
    
    // Mark first item as mastered
    const firstCheckbox = page.locator('tr:has-text("excellent") input[type="checkbox"]');
    await firstCheckbox.click();
    
    // Wait for update
    await page.waitForTimeout(500);
    
    // Verify the row is highlighted/styled as mastered
    const masteredRow = page.locator('tr:has-text("excellent")');
    await expect(masteredRow).toHaveClass(/table-success/);
    
    // Edit an item
    const editButton = page.locator('tr:has-text("superb") button:has-text("Edit")');
    await editButton.click();
    await expect(page.locator('.modal').last().locator('.modal-title')).toContainText('Edit Item', { timeout: 10000 });
    await page.fill('input[name="text"]', 'magnificent');
    await page.locator('.modal').last().locator('.modal-footer button:has-text("Update")').click();    
    // Wait for update
    await page.waitForTimeout(500);
    
    // Verify updated text
    await expect(page.locator('td:has-text("magnificent")')).toBeVisible();
    await expect(page.locator('td:has-text("superb")')).not.toBeVisible();
    
    // Delete an item
    // Set up dialog handler BEFORE clicking delete
    page.once('dialog', dialog => dialog.accept());
    
    const deleteItemButton = page.locator('tr:has-text("magnificent") button:has-text("Delete")');
    await deleteItemButton.click();
    
    // Wait for deletion
    await page.waitForTimeout(500);
    
    // Verify item is gone
    await expect(page.locator('td:has-text("magnificent")')).not.toBeVisible();
    
    // Verify first item still exists
    await expect(page.locator('td:has-text("excellent")')).toBeVisible();
    
    // Close details modal
    await page.click('.modal-footer button:has-text("Close")');
    await expect(page.locator('.modal-title')).not.toBeVisible({ timeout: 10000 });
    
    // Reload the page to get updated counts (UI doesn't auto-refresh)
    await page.reload();
    await page.waitForLoadState('networkidle');
    
    // Verify on main page - list shows 1 item and 1 mastered
    const listRow = page.locator(`tr:has-text("${listTitle}")`);
    await expect(listRow.locator('td').nth(2)).toContainText('1'); // Item count
    await expect(listRow.locator('td').nth(3)).toContainText('1'); // Mastered count
  });
});
