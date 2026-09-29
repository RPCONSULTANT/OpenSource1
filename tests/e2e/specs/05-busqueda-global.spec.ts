import { expect, test } from '@playwright/test';
import { captura, iniciarSesion, usuarios } from './ayuda';

test('paleta Ctrl+K: módulos al escribir, flechas, Enter y Esc', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  await page.goto('/');
  await page.keyboard.press('Control+k');
  await expect(page.locator('#busqueda-paleta')).toBeVisible();
  await page.keyboard.type('categoria');
  await expect(page.locator('#busqueda-paleta-lista [role="option"]').first()).toContainText('Categorías');
  // Espera a que termine la búsqueda de registros (debounce + /buscar/sugerencias) para que la imagen no diga "Buscando…".
  await expect(page.locator('#busqueda-paleta-lista')).not.toContainText('Buscando');
  await captura(page, '24-paleta-modulos', page.locator('#busqueda-paleta').getByText('Categorías de producto'));
  await page.keyboard.press('ArrowDown');
  await expect(page.locator('#busqueda-global')).toHaveAttribute('aria-activedescendant', 'busqueda-opcion-0');
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/categorias-producto/);
  // Se espera a que la navegación mejorada termine (enhancedload cierra la paleta) antes de reabrirla con "/".
  await expect(page.getByRole('heading', { name: /categorías/i }).first()).toBeVisible();
  await expect(page.locator('#busqueda-paleta')).toBeHidden();
  await page.locator('#busqueda-global').blur();
  await page.keyboard.press('/');
  await expect(page.locator('#busqueda-paleta')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.locator('#busqueda-paleta')).toBeHidden();
});

test('paleta tras navegación mejorada: ArrowDown ×2 selecciona la opción 1, no salta', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  // Carga completa en un módulo de Configuración (su grupo del menú queda abierto)...
  await page.goto('/unidades-medida');
  // ...y navegación mejorada de Blazor a otro módulo del grupo (clic en el menú, sin recarga completa).
  await page.getByTestId('nav-menu').locator('a[href="/terminos-pago"]').first().click();
  await expect(page).toHaveURL(/\/terminos-pago/);
  await expect(page.getByRole('heading', { name: /términos de pago/i }).first()).toBeVisible();
  await page.keyboard.press('Control+k');
  await expect(page.locator('#busqueda-paleta')).toBeVisible();
  await page.keyboard.type('a');
  await expect(page.locator('#busqueda-paleta-lista [role="option"]').nth(2)).toBeVisible();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(page.locator('#busqueda-global')).toHaveAttribute('aria-activedescendant', 'busqueda-opcion-1');
  await expect(page.locator('#busqueda-opcion-1')).toHaveAttribute('aria-selected', 'true');
  await page.keyboard.press('Escape');
});

test('búsqueda en servidor /buscar con módulos y registros', async ({ page }) => {
  await iniciarSesion(page, usuarios.admin);
  // "cliente" coincide con módulos (Clientes, Panel de clientes…) y con registros (clientes y documentos).
  await page.goto('/buscar?q=cliente');
  await expect(page.getByTestId('resultados-modulos')).toBeVisible();
  await expect(page.getByTestId('resultados-modulos').getByRole('link', { name: /clientes/i }).first()).toBeVisible();
  await expect(page.locator('[data-testid^="resultados-"]:not([data-testid="resultados-modulos"])').first()).toBeVisible();
  await captura(page, '25-buscar-resultados', /resultados para «cliente»/i);
});
