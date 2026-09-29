import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('navegación entre páginas sin destello azul', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/unidades-medida');
  const barra = page.locator('#nav-progress-bar');
  await expect(barra).toHaveCSS('height', '2px');
  expect(await page.locator('[class*="from-brand-700"]').count()).toBe(0);

  const opacidades: string[] = [];
  const navegar = page.locator('a[href="/terminos-pago"]').first().click();
  for (let i = 0; i < 8; i++) {
    opacidades.push(await barra.evaluate((el) => getComputedStyle(el).opacity));
    await page.waitForTimeout(25);
  }
  await navegar;
  await expect(page).toHaveURL(/\/terminos-pago/);
  await captura(page, '32-navegacion-sin-flash', /términos de pago/i);
  // En navegaciones locales rápidas (< 150 ms) la barra no llega a mostrarse.
  expect(opacidades.slice(0, 5).every((o) => Number(o) === 0)).toBe(true);
});
