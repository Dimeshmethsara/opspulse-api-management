import { expect, test } from '@playwright/test';

test('dashboard reads the real backend, filters services and fits a mobile screen', async ({ page }, testInfo) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'System overview' })).toBeVisible();
  await expect(page.locator('app-service-card')).toHaveCount(6);
  await expect(page.getByText('DEMO DATA')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('dashboard-desktop.png'), fullPage: true });
  await page.getByRole('searchbox', { name: 'Search services' }).fill('Payments');
  await expect(page.locator('app-service-card')).toHaveCount(1);
  await expect(page.locator('app-service-card')).toContainText('Payments API');
  await page.getByRole('searchbox', { name: 'Search services' }).clear();
  await page.getByRole('combobox', { name: 'Filter services by status' }).selectOption('Down');
  await expect(page.locator('app-service-card')).toHaveCount(1);
  await page.getByRole('button', { name: 'Resolved', exact: true }).click();
  await expect(page.locator('.incident-item')).toHaveCount(1);
  await expect(page.locator('.incident-item')).toContainText('Elevated payment processing latency');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole('combobox', { name: 'Filter services by status' }).selectOption('All');
  await page.getByRole('button', { name: 'All', exact: true }).click();
  await page.evaluate(() => {
    if (document.activeElement instanceof HTMLElement) document.activeElement.blur();
    window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
  });
  await expect(page.getByRole('heading', { name: 'System overview' })).toBeVisible();
  await expect(page.locator('app-service-card')).toHaveCount(6);
  expect(await page.locator('.sidebar').evaluate(element => element.getBoundingClientRect().top)).toBe(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('dashboard-mobile.png'), fullPage: true });
});

test('connection failures are visible without fabricated service data', async ({ page }) => {
  await page.route('**/api/status', route => route.abort());
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('Unable to reach the status API.');
  await expect(page.locator('app-service-card')).toHaveCount(0);
});
