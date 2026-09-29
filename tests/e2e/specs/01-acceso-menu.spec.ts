import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('login, inicio con grupos y menú por grupos del administrador', async ({ page }) => {
  await page.goto('/account/login');
  await captura(page, '01-login', /iniciar sesión/i);
  await iniciarSesion(page, usuarios.admin);
  await expect(page.getByTestId('grupos-inicio')).toBeVisible();
  await captura(page, '02-inicio', 'Facturación');
  await page.goto('/unidades-medida');
  await expect(page.locator('details[data-grupo="configuracion"]')).toHaveAttribute('open', '');
  await captura(page, '03-menu-grupos', 'Unidades de medida');
  await page.goto('/modulos/facturacion');
  await captura(page, '04-grupo-facturacion', 'Facturas');
});

test('ejecutor no ve Administración y el modo oscuro se conserva', async ({ page, context }) => {
  await iniciarSesion(page, usuarios.ejecutor);
  await expect(page.locator('details[data-grupo="administracion"]')).toHaveCount(0);
  await context.addCookies([{ name: 'axionerp-theme', value: 'dark', url: new URL('/', page.url()).toString() }]);
  await page.goto('/');
  await expect(page.locator('html')).toHaveClass(/dark/);
  await captura(page, '05-inicio-ejecutor-oscuro', 'Facturación');
});
