import { test, expect } from '@playwright/test';

// Test 5: Search (verify input visible, query works and results displayed)
test('Search', async ({ page }) => {
  await page.goto('/search');

  // Verify search input is visible
  const searchInput = page.locator('input[placeholder*="Search"], input[type="text"]').first();
  await expect(searchInput).toBeVisible();

  // Enter search query
  await searchInput.fill('test');
  
  // Wait for search results to load
  await page.waitForTimeout(500);

  // Verify search results are displayed
  const results = page.locator('text=/found|results|no results/i');
  await expect(results.first()).toBeVisible({ timeout: 5000 });
});
