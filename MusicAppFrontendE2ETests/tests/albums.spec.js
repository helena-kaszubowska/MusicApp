import { test, expect } from '@playwright/test';

// Test 1: Navigate to album details (verify URL, details, tracklist and Add to Library button)
test('NavigateToAlbumDetails', async ({ page }) => {
  await page.goto('/');

  // Wait for albums to load
  await page.waitForSelector('.grid img', { timeout: 10000 });

  // Click on the first album card title to navigate to album details
  const firstAlbumTitle = page.locator('.grid h3').first();
  await firstAlbumTitle.click();

  // Verify navigation to album details page
  await expect(page).toHaveURL(/.*\/album\/.*/, { timeout: 5000 });

  // Verify album title is displayed
  await expect(page.locator('h1').first()).toBeVisible({ timeout: 5000 });
  
  // Verify tracklist is displayed
  await expect(page.locator('text=/Tracks|track|song/i').first()).toBeVisible({ timeout: 5000 });
  
  // Verify Add to Library button is present
  await expect(page.getByRole('button', { name: /add to library/i }).first()).toBeVisible({ timeout: 5000 });
});
