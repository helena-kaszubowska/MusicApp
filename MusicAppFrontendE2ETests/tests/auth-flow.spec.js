import { test, expect } from '@playwright/test';

test.describe('Auth Flow', () => {
  // Test 2: Registration and login (verify album list is visible)
  test('RegistrationAndLogin', async ({ page }) => {
    const timestamp = Date.now();
    const email = `test${timestamp}@example.com`;
    const password = 'TestPassword123!';

    // 1. Registration
    await page.goto('/register');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.fill('input[placeholder*="confirm"]', password);
    await page.click('button[type="submit"]');

    // 2. Verify success message and redirect to login page
    await expect(page.locator('text=/registration successful/i')).toBeVisible({ timeout: 3000 });
    await page.waitForURL('**/login', { timeout: 5000 });

    // 3. Login
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button[type="submit"]');

    // 4. Redirect to home
    await page.waitForURL('**/', { timeout: 5000 });

    // 5. Check if album list is visible
    await expect(page.locator('text=Featured Albums')).toBeVisible({ timeout: 10000 });
  });

  // Test 3: Login with wrong password (verify error message and stay on login page)
  test('LoginWithWrongPassword', async ({ page }) => {
    await page.goto('/login');
    
    await page.fill('input[type="email"]', 'test@example.com');
    await page.fill('input[type="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');

    // Verify user stays on login page
    await expect(page).toHaveURL(/.*login/);

    // Verify error message is displayed
    await expect(page.locator('text=/Invalid email or password/i')).toBeVisible({ timeout: 5000 });
  });

  // Test 4: Logout (verify token removed from localStorage and redirect to login)
  test('Logout', async ({ page }) => {
    // 1. First, log in the user
    const timestamp = Date.now();
    const email = `test${timestamp}@example.com`;
    const password = 'TestPassword123!';

    // Registration and login
    await page.goto('/register');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.fill('input[placeholder*="confirm"]', password);
    await page.click('button[type="submit"]');
    await expect(page.locator('text=/registration successful/i')).toBeVisible({ timeout: 3000 });
    await page.waitForURL('**/login', { timeout: 5000 });
    
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button[type="submit"]');
    await page.waitForURL('**/', { timeout: 5000 });

    // 2. Click logout button
    const logoutButton = page.locator('text=Sign Out').first();
    await expect(logoutButton).toBeVisible({ timeout: 5000 });
    await logoutButton.click();
    
    // 3. Verify token is removed from localStorage
    const token = await page.evaluate(() => localStorage.getItem('authToken'));
    expect(token).toBeNull();
    
    // 4. Verify redirect to home page
    await expect(page).toHaveURL(/.*\/$/, { timeout: 5000 });
  });
});
